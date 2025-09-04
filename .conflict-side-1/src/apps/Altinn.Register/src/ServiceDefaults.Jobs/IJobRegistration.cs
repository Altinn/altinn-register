using System.Collections.Immutable;

namespace Altinn.Authorization.ServiceDefaults.Jobs;

/// <summary>
/// Represents a job registration that defines the configuration and metadata for a scheduled job.
/// </summary>
public interface IJobRegistration
{
    /// <summary>
    /// Gets the name of the job.
    /// </summary>
    public string JobName { get; }

    /// <summary>
    /// Gets the name of the lease that should be acquired before running the job.
    /// </summary>
    public string? LeaseName { get; }

    /// <summary>
    /// Gets the interval at which the job should run.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Gets the <see cref="JobHostLifecycles"/> that the job should run at.
    /// </summary>
    public JobHostLifecycles RunAt { get; }

    /// <summary>
    /// Gets the tags associated with the job.
    /// </summary>
    public ImmutableArray<string> Tags { get; }
}
