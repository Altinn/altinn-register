using System.Runtime.CompilerServices;
using Altinn.Authorization.ServiceDefaults.MassTransit.Commands;
using Azure.Messaging.ServiceBus.Administration;

namespace Altinn.Authorization.ServiceDefaults.MassTransit.AzureServiceBus;

/// <summary>
/// Implementation of <see cref="ICommandQueueStatsProvider"/> for Azure Service Bus.
/// </summary>
internal sealed class AzureServiceBusStatsProvider
    : ICommandQueueStatsProvider
{
    private readonly CommandQueueResolver _queueResolver;
    private readonly ServiceBusAdministrationClient _administrationClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusStatsProvider"/> class.
    /// </summary>
    public AzureServiceBusStatsProvider(
        CommandQueueResolver queueResolver,
        ServiceBusAdministrationClient administrationClient)
    {
        _queueResolver = queueResolver;
        _administrationClient = administrationClient;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<CommandQueueStats> GetCommandQueueStats(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queues = _queueResolver.QueueUris;
        
        await foreach (var props in _administrationClient.GetQueuesRuntimePropertiesAsync(cancellationToken))
        {
            foreach (var uri in queues)
            {
                if (uri.LocalPath == props.Name)
                {
                    ulong count = props.ActiveMessageCount switch
                    {
                        < 0 => 0,
                        var c => (ulong)c,
                    };
                    
                    yield return new(uri, count);
                    break;
                }
            }
        }
    }
}
