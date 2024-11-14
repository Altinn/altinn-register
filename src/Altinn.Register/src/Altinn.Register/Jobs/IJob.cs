#nullable enable

namespace Altinn.Register.Jobs;

/// <summary>
/// A job that can be run on a schedule or on host lifecycle events.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Gets the name of the job.
    /// </summary>
    string Name => GetType().Name;

    /// <summary>
    /// Runs the job.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    Task RunAsync(CancellationToken cancellationToken);
}
