using Altinn.Authorization.ServiceDefaults.MassTransit.Commands;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// Provides a no-op implementation of <see cref="ICommandQueueStatsProvider"/> that returns no command queue
/// statistics.
/// </summary>
/// <remarks>This provider can be used in scenarios where command queue statistics are not required or should be
/// disabled. All methods return empty results and do not perform any operations.</remarks>
internal sealed class NullStatsProvider
    : ICommandQueueStatsProvider
{
    /// <inheritdoc/>
    public IAsyncEnumerable<CommandQueueStats> GetCommandQueueStats(CancellationToken cancellationToken = default)
        => Enumerable.Instance;

    private sealed class Enumerable
        : IAsyncEnumerable<CommandQueueStats>
        , IAsyncEnumerator<CommandQueueStats>
    {
        public static Enumerable Instance { get; } = new Enumerable();

        public CommandQueueStats Current 
            => default!;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public IAsyncEnumerator<CommandQueueStats> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => this;

        public ValueTask<bool> MoveNextAsync()
            => ValueTask.FromResult(false);
    }
}
