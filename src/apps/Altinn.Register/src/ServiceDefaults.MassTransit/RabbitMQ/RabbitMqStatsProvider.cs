using System.Runtime.CompilerServices;
using Altinn.Authorization.ServiceDefaults.MassTransit.Commands;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Altinn.Authorization.ServiceDefaults.MassTransit.RabbitMQ;

/// <summary>
/// Implementation of <see cref="ICommandQueueStatsProvider"/> for RabbitMQ.
/// </summary>
internal sealed class RabbitMqStatsProvider
    : ICommandQueueStatsProvider
{
    private readonly CommandQueueResolver _queueResolver;
    private readonly ConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqStatsProvider"/> class.
    /// </summary>
    public RabbitMqStatsProvider(
        CommandQueueResolver queueResolver,
        ConnectionFactory connectionFactory)
    {
        _queueResolver = queueResolver;
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<CommandQueueStats> GetCommandQueueStats(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queues = _queueResolver.QueueUris;
        await using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var chan = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
        
        foreach (var queue in queues)
        {
            QueueDeclareOk declareOk;
            try
            {
                declareOk = await chan.QueueDeclarePassiveAsync(queue.LocalPath, cancellationToken);
            }
            catch (RabbitMQClientException)
            {
                continue;
            }
            
            yield return new CommandQueueStats(
                queue,
                declareOk.MessageCount);
        }
    }
}
