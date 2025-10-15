namespace Altinn.Authorization.ServiceDefaults.MassTransit.Commands;

/// <summary>
/// Represents statistics about the command queue, including an estimate of the number of messages currently in the
/// queue.
/// </summary>
/// <param name="QueueUri">The uri of the command queue.</param>
/// <param name="EstimatedMessagesInQueue">The estimated number of messages present in the command queue.</param>
public record CommandQueueStats(
    Uri QueueUri,
    ulong EstimatedMessagesInQueue);
