using System.ComponentModel.DataAnnotations;

namespace Altinn.Register.Core.ImportJobs;

/// <summary>
/// Import job queue status, consisting of the highest enqueued item and the highest known source item at the time of
/// last queue update.
/// </summary>
/// <remarks>
/// It's important that <c>default(ImportJobQueueStatus)</c> results in a value where both <see cref="EnqueuedMax"/> and
/// <see cref="SourceMax"/> are <c>0</c>, as this is used to indicate that the job has not yet been created.
/// </remarks>
public readonly record struct ImportJobQueueStatus
{
    /// <summary>
    /// Gets the highest enqueued item.
    /// </summary>
    public readonly required ulong EnqueuedMax { get; init; }

    /// <summary>
    /// Gets the highest known item at the source.
    /// </summary>
    public readonly required ulong SourceMax { get; init; }
}
