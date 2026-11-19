namespace Altinn.Register.Core.ImportJobs;

/// <summary>
/// Cleanup for saga state.
/// </summary>
public interface ISagaStateCleanup
{
    /// <summary>
    /// Deletes saga states that were completed, faulted, or in progress before the specified dates.
    /// </summary>
    /// <param name="batchSize">The maximum number of saga states to delete. Must be at least 100.</param>
    /// <param name="completedBefore">The cutoff date for completed saga states. States completed before this date will be deleted. If null,
    /// completed states are not deleted.</param>
    /// <param name="faultedBefore">The cutoff date for faulted saga states. States faulted before this date will be deleted. If null, faulted
    /// states are not deleted.</param>
    /// <param name="inProgressBefore">The cutoff date for in-progress saga states. States in progress before this date will be deleted. If null,
    /// in-progress states are not deleted.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The number of saga states deleted.</returns>
    public Task<int> DeleteOldStates(
        uint batchSize,
        DateTimeOffset? completedBefore = null,
        DateTimeOffset? faultedBefore = null,
        DateTimeOffset? inProgressBefore = null,
        CancellationToken cancellationToken = default);
}
