﻿#nullable enable

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using Altinn.Register.Core;
using Altinn.Register.Core.Leases;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Jobs;

/// <summary>
/// A hosted service that runs recurring jobs.
/// </summary>
internal sealed partial class RecurringJobHostedService
    : IHostedLifecycleService
    , IDisposable
{
    private readonly Counter<int> _jobsStarted;
    private readonly Counter<int> _jobsFailed;
    private readonly Counter<int> _jobsSucceeded;

    private readonly TimeProvider _timeProvider;
    private readonly LeaseManager _leaseManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RecurringJobHostedService> _logger;
    private readonly ImmutableArray<JobRegistration> _registrations;
    private Task? _schedulerTask;
    private CancellationTokenSource? _stoppingCts;

    private Task _runningScheduledJobs = Task.CompletedTask;

    /// <summary>
    /// For test use only.
    /// </summary>
    internal Task RunningScheduledJobs => Volatile.Read(ref _runningScheduledJobs);

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringJobHostedService"/> class.
    /// </summary>
    public RecurringJobHostedService(
        RegisterTelemetry telemetry,
        TimeProvider timeProvider,
        LeaseManager leaseManager,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RecurringJobHostedService> logger,
        IEnumerable<JobRegistration> registrations)
    {
        _jobsStarted = telemetry.CreateCounter<int>("register.jobs.started", unit: "jobs", description: "The number of jobs that have been started");
        _jobsFailed = telemetry.CreateCounter<int>("register.jobs.failed", unit: "jobs", description: "The number of jobs that have failed");
        _jobsSucceeded = telemetry.CreateCounter<int>("register.jobs.succeeded", unit: "jobs", description: "The number of jobs that have succeeded");

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

        // Then, run lifecycle jobs
        await RunLifecycleJobs(JobHostLifecycles.Stop, cancellationToken);
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
            else if (lifecycle == JobHostLifecycles.Starting)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot use leases at the starting lifecycle point, as database migrations might not have run yet");
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

        var jobTags = new TagList([
            new("job.name", name),
        ]);
        using var activity = RegisterTelemetry.StartActivity(ActivityKind.Internal, $"run job {name}");
        try
        {
            _jobsStarted.Add(1, in jobTags);
            Log.JobStarting(_logger, name);

            // update the start point to be more correct
            start = _timeProvider.GetTimestamp();
            await job.RunAsync(cancellationToken);
            var elapsed = _timeProvider.GetElapsedTime(start);

            _jobsSucceeded.Add(1, in jobTags);
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

            _jobsFailed.Add(1, in jobTags);
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
            using var tracker = TrackRunningScheduledJob();

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
                using var tracker = TrackRunningScheduledJob();
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

    /// <summary>
    /// Records a new job task as running. This is used in tests to be able to wait for all running jobs to complete.
    /// </summary>
    private IDisposable TrackRunningScheduledJob()
        => new ScheduledJobTracker(this);

    private sealed record JobRunResult(string Name, TimeSpan Duration, ExceptionDispatchInfo? Exception);

    private sealed class ScheduledJobTracker
        : IDisposable
    {
        private readonly TaskCompletionSource _tcs;
        private readonly RecurringJobHostedService _service;

        public ScheduledJobTracker(RecurringJobHostedService service)
        {
            _service = service;
            _tcs = new();

            ImmutableInterlocked.Update(
                ref _service._runningScheduledJobs,
                static (runningJob, jobTask) =>
                {
                    return (runningJob.IsCompleted, jobTask.IsCompleted) switch
                    {
                        (true, true) => Task.CompletedTask,
                        (true, false) => jobTask,
                        (false, true) => runningJob,
                        (false, false) => Task.WhenAll(runningJob, jobTask),
                    };
                },
                _tcs.Task);
        }

        public void Dispose()
        {
            if (_tcs.TrySetResult())
            {
                ImmutableInterlocked.Update(
                    ref _service._runningScheduledJobs,
                    static runningJob => runningJob.IsCompleted ? Task.CompletedTask : runningJob);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Job {JobName} starting")]
        public static partial void JobStarting(ILogger logger, string jobName);

        [LoggerMessage(1, LogLevel.Information, "Job {JobName} completed successfully in {Duration}")]
        public static partial void JobCompleted(ILogger logger, string jobName, TimeSpan duration);

        [LoggerMessage(2, LogLevel.Error, "Job {JobName} failed in {Duration}")]
        public static partial void JobFailed(ILogger logger, string jobName, TimeSpan duration, Exception exception);
    }
}