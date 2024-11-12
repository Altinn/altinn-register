namespace Altinn.Register.Core.ImportJobs;

/// <summary>
/// Import job queue status, consisting of the highest enqueued item and the highest known source item at the time of
/// last queue update.
/// </summary>
/// <remarks>
/// It's important that <c>default(ImportJobStatus)</c> results in a value where <see cref="EnqueuedMax"/>,
/// <see cref="SourceMax"/>, and <see cref="ProcessedMax"/> are <c>0</c>, as this is used to indicate that the job has
/// not yet been created.
/// </remarks>
public readonly record struct ImportJobStatus
{
    /// <inheritdoc cref="ImportJobQueueStatus.EnqueuedMax"/>
    public readonly required ulong EnqueuedMax { get; init; }

    /// <inheritdoc cref="ImportJobQueueStatus.SourceMax"/>
    public readonly required ulong SourceMax { get; init; }

    /// <inheritdoc cref="ImportJobProcessingStatus.ProcessedMax"/>
    public readonly required ulong ProcessedMax { get; init; }

    /// <summary>
    /// Gets the number of items that has been enqueued but not yet processed.
    /// </summary>
    public readonly ulong Unprocessed => EnqueuedMax - ProcessedMax;
}
