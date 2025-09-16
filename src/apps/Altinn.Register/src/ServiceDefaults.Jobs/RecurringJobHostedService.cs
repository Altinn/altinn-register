using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks.Sources;
using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Authorization.ServiceDefaults.Leases;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Jobs;

/// <summary>
/// A hosted service that runs recurring jobs.
/// </summary>
internal sealed partial class RecurringJobHostedService
    : IHostedLifecycleService
    , IAsyncDisposable
{
    private readonly Counter<int> _jobsStarted;
    private readonly Counter<int> _jobsFailed;
    private readonly Counter<int> _jobsSucceeded;
    private readonly Counter<int> _jobsSkipped;

    private readonly TimeProvider _timeProvider;
    private readonly LeaseManager _leaseManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RecurringJobHostedService> _logger;
    private readonly ImmutableArray<JobRegistration> _registrations;
    private readonly ImmutableArray<IJobCondition> _conditions;
    private readonly IOptionsMonitor<RecurringJobHostedSettings> _settings;
    private Task? _schedulerTask;
    private CancellationTokenSource? _stoppingCts;

    private readonly ScheduledJobTracker _tracker;
    private readonly DisposeHelper _dispose = new(nameof(RecurringJobHostedService));

    /// <summary>
    /// Wait for all scheduled jobs that are currently running to complete.
    /// </summary>
    /// <remarks>
    /// For test use only. Only 1 of these can exist at once (per instance of <see cref="RecurringJobHostedService"/>).
    /// If multiple tasks needs to wait for all scheduled running jobs, use the <see cref="ValueTask.AsTask"/> method
    /// to get a <see cref="Task"/> that can safely be shared.
    /// </remarks>
    internal ValueTask WaitForRunningScheduledJobs()
        => _tracker.WaitForRunningScheduledJobs();

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringJobHostedService"/> class.
    /// </summary>
    public RecurringJobHostedService(
        JobsTelemetry telemetry,
        TimeProvider timeProvider,
        LeaseManager leaseManager,
        IServiceProvider serviceProvider,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RecurringJobHostedService> logger,
        IEnumerable<JobRegistration> registrations,
        IEnumerable<IJobCondition> conditions,
        IOptionsMonitor<RecurringJobHostedSettings> settings)
    {
        _jobsStarted = telemetry.CreateCounter<int>("register.jobs.started", unit: "jobs", description: "The number of jobs that have been started");
        _jobsFailed = telemetry.CreateCounter<int>("register.jobs.failed", unit: "jobs", description: "The number of jobs that have failed");
        _jobsSucceeded = telemetry.CreateCounter<int>("register.jobs.succeeded", unit: "jobs", description: "The number of jobs that have succeeded");
        _jobsSkipped = telemetry.CreateCounter<int>("register.jobs.skipped", unit: "jobs", description: "The number of jobs that have been skipped");

        _timeProvider = timeProvider;
        _leaseManager = leaseManager;
        _serviceProvider = serviceProvider;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _settings = settings;
        _registrations = registrations.ToImmutableArray();
        _conditions = conditions.ToImmutableArray();
        _tracker = new(_timeProvider);
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

        if (_settings.CurrentValue.DisableScheduler)
        {
            return;
        }

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
        // https://github.com/dotnet/aspnetcore/issues/40271
        if (_dispose.IsDisposed)
        {
            return;
        }

        // First, stop the scheduler
        await StopScheduler(cancellationToken);

        // Then, run lifecycle jobs
        await RunLifecycleJobs(JobHostLifecycles.Stop, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
        => _dispose.DisposeAsync(
            this,
            static async (self) =>
            {
                if (self._stoppingCts is not null)
                {
                    await self._stoppingCts.CancelAsync().WaitAsync(TimeSpan.FromMinutes(1));
                }

                if (self._schedulerTask is not null)
                {
                    await self._schedulerTask.WaitAsync(TimeSpan.FromMinutes(1)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }

                self._stoppingCts?.Dispose();
                self._stoppingCts = null;

                self._tracker.Dispose();
            });

    private async Task StopScheduler(CancellationToken cancellationToken)
    {
        // Stop called without start, or scheduler was disabled
        if (_schedulerTask is null)
        {
            return;
        }

        try
        {
            await _stoppingCts!.CancelAsync().WaitAsync(TimeSpan.FromMinutes(1), cancellationToken);
        }
        finally
        {
            await _schedulerTask.WaitAsync(cancellationToken).WaitAsync(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task RunLifecycleJobs(
        JobHostLifecycles lifecycle,
        CancellationToken cancellationToken)
    {
        // https://github.com/dotnet/aspnetcore/issues/40271
        if (_dispose.IsDisposed)
        {
            return;
        }

        _dispose.EnsureNotDisposed();

        var registrations = WhenReady(_registrations.Where(r => r.RunAt.HasFlag(lifecycle)), cancellationToken);
        await foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isEnabled = await IsEnabled(registration, throwOnException: true, lifecycle.ToString(), cancellationToken);
            if (!isEnabled)
            {
                continue;
            }

            if (registration.LeaseName is null)
            {
                await RunJob(registration, throwOnException: true, lifecycle.ToString(), cancellationToken);
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
                    await RunJob(registration, throwOnException: true, lifecycle.ToString(), lease.Token);
                }
                else
                {
                    Log.LeaseNotAcquired(_logger, jobName: registration.JobName, registration.LeaseName, lifecycle);
                }
            }
        }
    }

    private Task<JobRunResult> RunJob(JobRegistration registration, bool throwOnException, string source, CancellationToken cancellationToken)
    {
        return Task.Run(
            async () =>
            {
                Activity.Current = null;

                IJob? job = null;
                try
                {
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();
                    job = registration.Create(scope.ServiceProvider);
                    var name = registration.JobName;
                    var jobTags = new TagList([
                        new("job.name", name),
                        new("job.source", source),
                    ]);

                    using var outerActivity = JobsTelemetry.StartActivity($"job {name}", ActivityKind.Internal, tags: in jobTags);

                    try
                    {
                        using var shouldRunActivity = JobsTelemetry.StartActivity($"should run job {name}", ActivityKind.Internal, tags: in jobTags);
                        var shouldRun = await job.ShouldRun(cancellationToken);
                        if (!shouldRun)
                        {
                            _jobsSkipped.Add(1, in jobTags);
                            Log.JobSkipped(_logger, name);
                            return JobRunResult.Skipped(name);
                        }
                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        _jobsFailed.Add(1, in jobTags);
                        Log.JobFailed(_logger, name, duration: TimeSpan.Zero, e);
                        outerActivity?.SetStatus(ActivityStatusCode.Error, e.Message);

                        if (throwOnException)
                        {
                            throw;
                        }

                        var exception = ExceptionDispatchInfo.Capture(e);
                        return JobRunResult.Failure(name, duration: TimeSpan.Zero, exception);
                    }

                    var start = _timeProvider.GetTimestamp();
                    using var activity = JobsTelemetry.StartActivity($"run job {name}", ActivityKind.Internal, tags: in jobTags);
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
                        return JobRunResult.Success(name, elapsed);
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
                        return JobRunResult.Failure(name, elapsed, exception);
                    }
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
            },
            cancellationToken);
    }

    private async ValueTask<bool> IsEnabled(JobRegistration registration, bool throwOnException, string source, CancellationToken cancellationToken)
    {
        using var activity = JobsTelemetry.StartActivity($"check job-registration enabled", ActivityKind.Internal, tags: [new("job.source", source)]);

        foreach (var condition in _conditions)
        {
            if (!AppliesTo(condition, registration))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool canRun;
            using var conditionActivity = JobsTelemetry.StartActivity($"check job-registration condition", ActivityKind.Internal, tags: [new("condition.name", condition.Name)]);
            try
            {
                canRun = await condition.ShouldRun(cancellationToken);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.JobConditionFailed(_logger, jobName: registration.JobName, condition.Name, e);
                conditionActivity?.SetStatus(ActivityStatusCode.Error);

                if (throwOnException)
                {
                    throw;
                }

                return false;
            }

            if (!canRun)
            {
                Log.JobBlockedByCondition(_logger, jobName: registration.JobName, condition.Name);

                return false;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var isEnabled = await registration.Enabled(_serviceProvider, cancellationToken);

            if (!isEnabled)
            {
                Log.JobIsDisabledByDelegate(_logger, jobName: registration.JobName);
            }

            return isEnabled;
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (Exception e)
        {
            Log.JobIsEnabledFailed(_logger, jobName: registration.JobName, e);

            if (throwOnException)
            {
                throw;
            }

            return false;
        }

        static bool AppliesTo(IJobCondition condition, JobRegistration registration)
        {
            foreach (var tag in condition.JobTags)
            {
                if (!registration.HasTag(tag))
                {
                    return false;
                }
            }

            return true;
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

            var scheduler = _tracker.CreateScheduler();
            var leaseName = registration.LeaseName;
            if (leaseName is null)
            {
                tasks.Add(RunNormalScheduler(scheduler, interval, registration, cancellationToken));
            }
            else
            {
                tasks.Add(RunLeaseScheduler(scheduler, leaseName, interval, registration, cancellationToken));
            }
        }

        return Task.WhenAll(tasks);
    }

    private async Task RunNormalScheduler(
        JobScheduler scheduler,
        TimeSpan interval,
        JobRegistration registration,
        CancellationToken cancellationToken)
    {
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
        using var _scheduler = scheduler;
#pragma warning restore SA1312 // Variable names should begin with lower-case letter

        var start = _timeProvider.GetTimestamp();
        await scheduler.WaitForReady(registration, _serviceProvider, cancellationToken);
        var elapsed = _timeProvider.GetElapsedTime(start);

        while (!cancellationToken.IsCancellationRequested)
        {
            // we wait first - if the job should also be ran at startup, set RunAt to the appropriate lifecycle
            await scheduler.Sleep(interval - elapsed, cancellationToken).ConfigureAwait(false);
            elapsed = TimeSpan.Zero;

            // we check if the job is enabled. If it isn't, we sleep for 10x the interval to reduce usage for disabled jobs.
            // this still allows enabling jobs via feature-flags without having to restart the host.
            var isEnabled = await IsEnabled(registration, throwOnException: false, "scheduler", cancellationToken);
            if (!isEnabled)
            {
                // 8x, cause we already waited for `interval` once, and we continue so it will wait for another `interval` before checking again
                await scheduler.Sleep(interval * 8, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // run the job
            await RunJob(registration, throwOnException: false, "scheduler", cancellationToken);
        }
    }

    private async Task RunLeaseScheduler(
        JobScheduler scheduler,
        string leaseName,
        TimeSpan interval,
        JobRegistration registration,
        CancellationToken cancellationToken)
    {
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
        using var _scheduler = scheduler;
#pragma warning restore SA1312 // Variable names should begin with lower-case letter

        await scheduler.WaitForReady(registration, _serviceProvider, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow();
            DateTimeOffset lastCompleted;

            {
                // check if the job is enabled. If it isn't, we can skip the lease acquisition.
                var isEnabled = await IsEnabled(registration, throwOnException: false, "lease-scheduler", cancellationToken);
                if (!isEnabled)
                {
                    // here, we just sleep for 10x the interval to reduce usage for disabled jobs.
                    // this still allows enabling jobs via feature-flags without having to restart the host.
                    await scheduler.Sleep(interval * 10, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // we try to grab the lease first, as it might have been longer than `interval` time since we last ran
                await using var lease = await _leaseManager.AcquireLease(
                    leaseName,
                    (prev) => prev.LastReleasedAt is null || prev.LastReleasedAt.Value + interval <= now,
                    cancellationToken);

                if (lease.Acquired)
                {
                    // if we acquired the lease, run the job
                    await RunJob(registration, throwOnException: false, "lease-scheduler", lease.Token);
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
                // note, we grab the new delay task here to make sure we schedule the next run before releasing the tracker
                await scheduler.Sleep(tilThen, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async IAsyncEnumerable<JobRegistration> WhenReady(IEnumerable<JobRegistration> registrations, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<Task<JobRegistration>>? pending = null;

        foreach (var registration in registrations)
        {
            var ready = registration.WaitForReady(_serviceProvider, cancellationToken);
            if (!ready.IsCompleted)
            {
                pending ??= [];
                pending.Add(WaitFor(ready, registration));
                
                continue;
            }

            await ready; // propagates exceptions
            yield return registration;
        }

        if (pending is null)
        {
            yield break;
        }

        await foreach (var complete in Task.WhenEach(pending))
        {
            yield return await complete;
        }

        [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Task is not foreign")]
        static async Task<JobRegistration> WaitFor(Task ready, JobRegistration registration)
        {
            await ready;
            return registration;
        }
    }

    private sealed record JobRunResult
    {
        public static JobRunResult Disabled { get; } = new(Outcome.Disabled, exception: null);
        
        public static JobRunResult Success(string name, TimeSpan duration)
            => new(Outcome.Success, exception: null) { Name = name, Duration = duration };

        public static JobRunResult Failure(string name, TimeSpan duration, ExceptionDispatchInfo exception)
            => new(Outcome.Failure, exception) { Name = name, Duration = duration };

        public static JobRunResult Skipped(string name)
            => new(Outcome.Skipped, exception: null) { Name = name };

        private readonly Outcome _outcome;
        private readonly ExceptionDispatchInfo? _exception;

        public string? Name { get; private init; }

        public TimeSpan Duration { get; private init; }

        private JobRunResult(Outcome outcome, ExceptionDispatchInfo? exception)
        {
            _outcome = outcome;
            _exception = exception;
        }

        private enum Outcome
        {
            /// <summary>
            /// Indicates that <see cref="JobRegistration.Enabled(IServiceProvider, CancellationToken)"/> returned <see langword="false"/>.
            /// </summary>
            Disabled,

            /// <summary>
            /// Indicates that <see cref="IJob.ShouldRun(CancellationToken)"/> returned <see langword="false"/>.
            /// </summary>
            Skipped,

            /// <summary>
            /// Indicates that the job ran successfully.
            /// </summary>
            Success,

            /// <summary>
            /// Indicates that the job ran, but failed with an exception.
            /// </summary>
            Failure,
        }
    }

    private sealed class ScheduledJobTracker
        : IValueTaskSource
        , IDisposable
    {
        private readonly TimeProvider _timeProvider;
        private readonly Lock _lock = new();
        private ManualResetValueTaskSourceCore<object?> _source; // mutable struct; do not make this readonly
        private int _totalSchedulers;
        private int _awakeSchedulers;

        public ScheduledJobTracker(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;

            // initially set
            _source.SetResult(null);
            _source.RunContinuationsAsynchronously = true;
        }

        public JobScheduler CreateScheduler()
        {
            lock (_lock)
            {
                _totalSchedulers++;
            }

            return new(this, _timeProvider);
        }

        public void TrackAwake() 
        {
            lock (_lock)
            {
                if (_awakeSchedulers == 0)
                {
                    _source.Reset();
                }

                _awakeSchedulers++;
                Debug.Assert(_awakeSchedulers <= _totalSchedulers);
            }
        }

        public void TrackSleeping()
        {
            lock (_lock)
            {
                _awakeSchedulers--;
                Debug.Assert(_awakeSchedulers >= 0);

                if (_awakeSchedulers == 0)
                {
                    _source.SetResult(null);
                }
            }
        }

        /// <remarks>
        /// This method should only be called by schedulers that are in the awake state.
        /// </remarks>
        public void Unregister()
        {
            lock (_lock)
            {
                _awakeSchedulers--;
                _totalSchedulers--;
                Debug.Assert(_totalSchedulers >= 0);
                Debug.Assert(_awakeSchedulers >= 0);
                Debug.Assert(_awakeSchedulers <= _totalSchedulers);
            }
        }

        public void Dispose()
        {
            _source.Reset();
            Debug.Assert(_awakeSchedulers == 0);
            Debug.Assert(_totalSchedulers == 0);
        }

        public ValueTask WaitForRunningScheduledJobs()
            => new(this, _source.Version);

        void IValueTaskSource.GetResult(short token)
            => _source.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
            => _source.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _source.OnCompleted(continuation, state, token, flags);
    }

    private sealed class JobScheduler
        : IValueTaskSource
        , IDisposable
    {
        private const byte STATE_RUNNING = 0;
        private const byte STATE_SLEEPING = 1;
        private const byte STATE_DISPOSED = 2;
        private const byte STATE_REGISTERING = 3;

        private static readonly TimerCallback _timerCallback = static state =>
        {
            var timer = (JobScheduler)state!;
            timer.TimerCallback();
        };

        private static readonly Action<object?, CancellationToken> _cancellationCallback = static (state, ct) =>
        {
            var timer = (JobScheduler)state!;
            timer.CancellationCallback(ct);
        };

        private readonly Lock _lock = new();
        private readonly ITimer _timer;
        private readonly ScheduledJobTracker _tracker;
        private ManualResetValueTaskSourceCore<object?> _source; // mutable struct; do not make this readonly
        private CancellationTokenRegistration _cancellationRegistration;
        private byte _state = STATE_RUNNING;

        public JobScheduler(ScheduledJobTracker tracker, TimeProvider timeProvider)
        {
            _timer = timeProvider.CreateTimer(_timerCallback, this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _tracker = tracker;
            _tracker.TrackAwake();
        }

        public ValueTask Sleep(TimeSpan duration, CancellationToken cancellationToken)
        {
            if (duration <= TimeSpan.Zero)
            {
                return ValueTask.CompletedTask;
            }

            _source.Reset();
            _timer.Change(duration, Timeout.InfiniteTimeSpan);
            if (cancellationToken.CanBeCanceled)
            {
                lock (_lock)
                {
                    Debug.Assert(_state == STATE_RUNNING);
                    _state = STATE_REGISTERING;
                }

                _cancellationRegistration = cancellationToken.UnsafeRegister(_cancellationCallback, this);

                lock (_lock)
                {
                    Debug.Assert(_state == STATE_REGISTERING);
                    _state = STATE_RUNNING;
                }
            }

            return new(this, _source.Version);
        }

        public ValueTask WaitForReady(JobRegistration registration, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var task = registration.WaitForReady(serviceProvider, cancellationToken);
            if (task.IsCompleted)
            {
                return new(task);
            }

            var outerTask = WaitFor(this, task, cancellationToken);
            return new(outerTask);

            [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Not a foreign task")]
            static async Task WaitFor(JobScheduler scheduler, Task inner, CancellationToken cancellationToken)
            {
                lock (scheduler._lock)
                {
                    // this should be the first thing that happens to a scheduler, and schedulers starts as running
                    Debug.Assert(scheduler._state == STATE_RUNNING);
                    scheduler._state = STATE_SLEEPING;
                    scheduler._tracker.TrackSleeping();
                }

                try
                {
                    await inner;
                }
                finally
                {
                    lock (scheduler._lock)
                    {
                        // still nothing should have invoked the scheduler
                        Debug.Assert(scheduler._state == STATE_SLEEPING);
                        scheduler._state = STATE_RUNNING;
                        scheduler._tracker.TrackAwake();
                    }
                }
            }
        }

        private void TimerCallback()
        {
            var transition = false;
            lock (_lock)
            {
                if (_state == STATE_SLEEPING)
                {
                    _state = STATE_RUNNING;
                    transition = true;
                    _tracker.TrackAwake();
                }
            }

            if (transition)
            {
                _cancellationRegistration.Unregister();
                _source.SetResult(null);
            }
        }

        private void CancellationCallback(CancellationToken cancellationToken)
        {
            var transition = false;
            lock (_lock)
            {
                if (_state == STATE_SLEEPING)
                {
                    _state = STATE_RUNNING;
                    transition = true;
                    _tracker.TrackAwake();
                }
                else if (_state == STATE_REGISTERING)
                {
                    // this happens if cancellation was requested before we started sleeping
                    transition = true;
                }
            }

            if (transition)
            {
                _cancellationRegistration.Unregister();
                _source.SetException(new OperationCanceledException(cancellationToken));
            }
        }

        void IValueTaskSource.GetResult(short token)
            => _source.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
            => _source.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            lock (_lock)
            {
                if (_state == STATE_RUNNING)
                {
                    _state = STATE_SLEEPING;
                    _tracker.TrackSleeping();
                }
            }

            _source.OnCompleted(continuation, state, token, flags);
        }

        void IDisposable.Dispose()
        {
            int oldState;
            lock (_lock)
            {
                oldState = _state;
                _state = STATE_DISPOSED;

                if (oldState == STATE_SLEEPING)
                {
                    _tracker.TrackAwake();
                }
            }

            if (oldState == STATE_DISPOSED)
            {
                return;
            }

            _cancellationRegistration.Dispose();
            _timer.Dispose();
            _source.Reset();
            _tracker.Unregister();
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

        [LoggerMessage(3, LogLevel.Information, "Skipping job {JobName} as lease {LeaseName} not acquired for lifecycle event {Lifecycle}")]
        public static partial void LeaseNotAcquired(ILogger logger, string jobName, string leaseName, JobHostLifecycles lifecycle);

        [LoggerMessage(4, LogLevel.Error, "Job registration enabled check for {JobName} failed")]
        public static partial void JobIsEnabledFailed(ILogger logger, string jobName, Exception exception);

        [LoggerMessage(5, LogLevel.Information, "Job {JobName} skipped as it is not enabled")]
        public static partial void JobSkipped(ILogger logger, string jobName);

        [LoggerMessage(6, LogLevel.Error, "Job condition {ConditionName} for job {JobName} failed")]
        public static partial void JobConditionFailed(ILogger logger, string jobName, string conditionName, Exception exception);

        [LoggerMessage(7, LogLevel.Information, "Job {JobName} blocked by condition {ConditionName}")]
        public static partial void JobBlockedByCondition(ILogger logger, string jobName, string conditionName);

        [LoggerMessage(8, LogLevel.Information, "Job {JobName} is disabled by registration enabled delegate")]
        public static partial void JobIsDisabledByDelegate(ILogger logger, string jobName);
    }
}
