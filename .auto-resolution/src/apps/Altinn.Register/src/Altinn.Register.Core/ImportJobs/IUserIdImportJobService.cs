using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.ImportJobs;

/// <summary>
/// Service interface for the user-id import-job.
/// </summary>
public interface IUserIdImportJobService
{
    /// <summary>
    /// Get parties without (active) user-id and job-state for the given jobId and partyTypes.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="partyTypes">The set of <see cref="PartyRecordType"/>s to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>An async enumerable of <see cref="PartyRecord.PartyUuid"/> and <see cref="PartyRecord.PartyType"/> pairs.</returns>
    IAsyncEnumerable<(Guid PartyUuid, PartyRecordType PartyType)> GetPartiesWithoutUserIdAndJobState(string jobId, IReadOnlySet<PartyRecordType> partyTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the job state for all parties that has already been assigned a user-id.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    Task ClearJobStateForPartiesWithUserId(string jobId, CancellationToken cancellationToken = default);
}
