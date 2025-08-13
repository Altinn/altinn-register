namespace Altinn.Authorization.ServiceDefaults.Jobs;

/// <summary>
/// A registration for a job.
/// </summary>
public abstract class JobRegistration(
    string? leaseName,
    TimeSpan interval,
    JobHostLifecycles runAt,
    Func<IServiceProvider, CancellationToken, ValueTask<bool>>? enabled,
    Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady)
{
    private readonly Lock _lock = new();
    private Task? _ready;

    /// <summary>
    /// Creates a new instance of the job.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>A new instance of <see cref="IJob"/>.</returns>
    public abstract IJob Create(IServiceProvider services);

    /// <summary>
    /// Gets the name of the lease that should be acquired before running the job.
    /// </summary>
    public string? LeaseName => leaseName;

    /// <summary>
    /// Gets the interval at which the job should run.
    /// </summary>
    public TimeSpan Interval => interval;

    /// <summary>
    /// Gets the <see cref="JobHostLifecycles"/> that the job should run at.
    /// </summary>
    public JobHostLifecycles RunAt => runAt;

    /// <summary>
    /// Checks if the job is enabled or not.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// Enabled runs in singleton scope, so it should not resolve scoped services. Should never throw.
    /// 
    /// Note: this is an earlier check than <see cref="IJob.ShouldRun(CancellationToken)"/>, which
    /// technically runs in the scope of the job itself. This means that if this returns <see langword="false"/>,
    /// the job will not be created or run at all, and the job's <see cref="IJob.ShouldRun(CancellationToken)"/> 
    /// method will not be called. <see cref="Enabled(IServiceProvider, CancellationToken)"/> is typically used to
    /// check if the job is enabled based on configuration or other conditions that do not require the job to be
    /// instantiated.
    /// </remarks>
    /// <returns><see langword="true"/> if the job is enabled, otherwise <see langword="false"/>.</returns>
    public ValueTask<bool> Enabled(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        return enabled switch
        {
            null => ValueTask.FromResult(true),
            { } fn => fn(services, cancellationToken),
        };
    }

    /// <summary>
    /// Waits for the job to be ready to run. This is only ran once per job registration.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// Wait for ready runs in singleton scope, so it should not resolve scoped services. Should never throw.
    /// </remarks>
    public Task WaitForReady(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        Task? task;
        lock (_lock)
        {
            task = _ready;
            if (task is null)
            {
                task = waitForReady is null ? Task.CompletedTask : waitForReady(services, cancellationToken).AsTask();
                _ready = task;
            }
        }

        return task.WaitAsync(cancellationToken);
    }
}
