namespace Altinn.Authorization.ServiceDefaults.MassTransit.Commands;

/// <summary>
/// Defines a provider for retrieving statistics related to command queue operations.
/// </summary>
public interface ICommandQueueStatsProvider
{
    /// <summary>
    /// Asynchronously retrieves statistics for all command queues.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>An asynchronous sequence of <see cref="CommandQueueStats"/> objects, each representing statistics for a command
    /// queue.</returns>
    public IAsyncEnumerable<CommandQueueStats> GetCommandQueueStats(CancellationToken cancellationToken = default);
}
