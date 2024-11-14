#nullable enable

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Altinn.Register.Core;
using Altinn.Register.Core.Leases;

namespace Altinn.Register.Jobs;

/// <summary>
/// A hosted service that runs recurring jobs.
/// </summary>
internal sealed partial class RecurringJobHostedService
    : IHostedLifecycleService
    , IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly LeaseManager _leaseManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RecurringJobHostedService> _logger;
    private readonly ImmutableArray<JobRegistration> _registrations;
    private Task? _schedulerTask;
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringJobHostedService"/> class.
    /// </summary>
    public RecurringJobHostedService(
        TimeProvider timeProvider,
        LeaseManager leaseManager,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RecurringJobHostedService> logger,
        IEnumerable<JobRegistration> registrations)
    {
        _timeProvider = timeProvider;
        _leaseManager = leaseManager;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _registrations = registrations.ToImmutableArray();
    }

    /// <inheritdoc/>
    public Task StartingAsync(CancellationToken cancellationToken)
        => RunLifecycleJobs(JobHostLifecycles.Starting, cancellationToken);

    /// <inheritdoc/>
    public Task StartedAsync(CancellationToken cancellationToken)
        => RunLifecycleJobs(JobHostLifecycles.Started, cancellationToken);

    /// <inheritdoc/>
    public Task StoppingAsync(CancellationToken cancellationToken)
        => RunLifecycleJobs(JobHostLifecycles.Stopping, cancellationToken);

    /// <inheritdoc/>
    public Task StoppedAsync(CancellationToken cancellationToken)
        => RunLifecycleJobs(JobHostLifecycles.Stopped, cancellationToken);

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // First, run all lifecycle jobs that should run at the start of the host
        await RunLifecycleJobs(JobHostLifecycles.Start, cancellationToken);

        // Create linked token to allow cancelling executing task from provided token
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Store the task we're executing
        _schedulerTask = RunScheduler(_stoppingCts.Token);

        // If the task is completed then await it, this will bubble cancellation and failure to the caller
        if (_schedulerTask.IsCompleted)
        {
            await _schedulerTask;
        }

        // Otherwise it's running
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // First, stop the scheduler
        await StopScheduler(cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stoppingCts?.Cancel();
    }

    private async Task StopScheduler(CancellationToken cancellationToken)
    {
        // Stop called without start
        if (_schedulerTask is null)
        {
            return;
        }

        try
        {
            await _stoppingCts!.CancelAsync();
        }
        finally
        {
            await _schedulerTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task RunLifecycleJobs(
        JobHostLifecycles lifecycle,
        CancellationToken cancellationToken)
    {
        var registrations = _registrations.Where(r => r.RunAt.HasFlag(lifecycle));
        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (registration.LeaseName is null)
            {
                await RunJob(registration, throwOnException: true, cancellationToken);
            }
            else
            {
                await using var lease = await _leaseManager.AcquireLease(registration.LeaseName, cancellationToken);
                if (lease.Acquired)
                {
                    await RunJob(registration, throwOnException: true, lease.Token);
                }
            }
        }
    }

    private async Task<JobRunResult> RunJob(JobRegistration registration, bool throwOnException, CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var job = registration.Create(scope.ServiceProvider);

        var start = _timeProvider.GetTimestamp();
        var name = job.Name;

        using var activity = RegisterActivitySource.StartActivity(ActivityKind.Internal, $"run job {name}");
        try
        {
            // update the start point to be more correct
            start = _timeProvider.GetTimestamp();
            await job.RunAsync(cancellationToken);
            var elapsed = _timeProvider.GetElapsedTime(start);

            Log.JobCompleted(_logger, name, elapsed);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new JobRunResult(name, elapsed, null);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (Exception e)
        {
            var elapsed = _timeProvider.GetElapsedTime(start);

            Log.JobFailed(_logger, name, elapsed, e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            if (throwOnException)
            {
                throw;
            }

            var exception = ExceptionDispatchInfo.Capture(e);
            return new JobRunResult(name, elapsed, exception);
        }
        finally
        {
            if (job is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (job is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private Task RunScheduler(CancellationToken cancellationToken)
    {
        var registrations = _registrations.Where(r => r.Interval > TimeSpan.Zero);
        var tasks = new List<Task>(_registrations.Length);

        foreach (var registration in registrations)
        {
            var interval = registration.Interval;
            if (interval < TimeSpan.FromSeconds(30))
            {
                throw new InvalidOperationException($"Minimum interval for background jobs is currently set at 30 seconds");
            }

            var leaseName = registration.LeaseName;
            if (leaseName is null)
            {
                tasks.Add(RunNormalScheduler(interval, registration, cancellationToken));
            }
            else
            {
                tasks.Add(RunLeaseScheduler(leaseName, interval, registration, cancellationToken));
            }
        }

        return Task.WhenAll(tasks);
    }

    private async Task RunNormalScheduler(TimeSpan interval, JobRegistration registration, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // we wait first - if the job should also be ran at startup, set RunAt to the appropriate lifecycle
            await _timeProvider.Delay(interval, cancellationToken);

            // run the job
            await RunJob(registration, throwOnException: false, cancellationToken);
        }
    }

    private async Task RunLeaseScheduler(string leaseName, TimeSpan interval, JobRegistration registration, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // we try to grab the lease first, as it might have been longer than `interval` time since we last ran
            var now = _timeProvider.GetUtcNow();
            
            DateTimeOffset lastCompleted;
            {
                await using var lease = await _leaseManager.AcquireLease(leaseName, (prev) => prev.LastReleasedAt + interval < now, cancellationToken);

                if (lease.Acquired)
                {
                    // if we acquired the lease, run the job
                    await RunJob(registration, throwOnException: false, lease.Token);
                    var releaseResult = await lease.Release(cancellationToken);

                    Debug.Assert(releaseResult is not null);
                    Debug.Assert(releaseResult.LastReleasedAt.HasValue);
                    lastCompleted = releaseResult.LastReleasedAt.Value;
                }
                else
                {
                    // else record when it was last ran
                    lastCompleted = lease.Expires;
                    if (lease.LastReleasedAt.HasValue && lease.LastReleasedAt > lastCompleted)
                    {
                        lastCompleted = lease.LastReleasedAt.Value;
                    }
                }
            }

            // calculate when next the job should run, and wait for that, then loop
            var nextStart = lastCompleted + interval;
            var tilThen = nextStart - now;

            if (tilThen > TimeSpan.Zero)
            {
                await _timeProvider.Delay(tilThen, cancellationToken);
            }
        }
    }

    private record JobRunResult(string Name, TimeSpan Duration, ExceptionDispatchInfo? Exception);

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Job {JobName} completed successfully in {Duration}")]
        public static partial void JobCompleted(ILogger logger, string jobName, TimeSpan duration);

        [LoggerMessage(1, LogLevel.Warning, "Job {JobName} failed in {Duration}")]
        public static partial void JobFailed(ILogger logger, string jobName, TimeSpan duration, Exception exception);
    }
}
