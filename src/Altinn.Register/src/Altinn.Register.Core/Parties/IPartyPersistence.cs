using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Persistence service for parties.
/// </summary>
public interface IPartyPersistence
{
    /// <summary>
    /// Gets a single party (as a <see cref="IAsyncEnumerable{T}"/>
    /// of the party and optionally it's direct child units if requested).
    /// </summary>
    /// <param name="partyUuid">The party uuid.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing 0 items if the party
    /// is not found, 1 parent <see cref="PartyRecord"/>, and an unbounded
    /// amount of child units if requested.
    /// </returns>
    public IAsyncEnumerable<PartyRecord> GetPartyById(
        Guid partyUuid,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single party (as a <see cref="IAsyncEnumerable{T}"/>
    /// of the party and optionally it's direct child units if requested).
    /// </summary>
    /// <param name="partyId">The party id.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing 0 items if the party
    /// is not found, 1 parent <see cref="PartyRecord"/>, and an unbounded
    /// amount of child units if requested.
    /// </returns>
    public IAsyncEnumerable<PartyRecord> GetPartyById(
        int partyId,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);
}
