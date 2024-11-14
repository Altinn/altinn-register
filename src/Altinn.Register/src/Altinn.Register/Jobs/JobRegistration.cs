#nullable enable

namespace Altinn.Register.Jobs;

/// <summary>
/// A registration for a job.
/// </summary>
public abstract class JobRegistration(
    string? leaseName,
    TimeSpan interval,
    JobHostLifecycles runAt)
{
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
}
