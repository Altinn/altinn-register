#nullable enable

namespace Altinn.Register.Jobs;

/// <summary>
/// A registration for a job.
/// </summary>
public abstract class JobRegistration(
    string? leaseName,
    TimeSpan interval,
    JobHostLifecycles runAt,
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
    /// Waits for the job to be ready to run. This is only ran once per job registration.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// Wait for ready runs in singleton scope, so it should not resolve scoped services.
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
