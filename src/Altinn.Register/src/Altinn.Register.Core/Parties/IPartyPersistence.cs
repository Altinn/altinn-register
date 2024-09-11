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

    /// <summary>
    /// Gets a single organization (as a <see cref="IAsyncEnumerable{T}"/>
    /// of the organization and optionally it's direct child units if requested).
    /// </summary>
    /// <param name="identifier">The organization identifier.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing 0 items if the party
    /// is not found, 1 parent <see cref="OrganizationRecord"/>, and an unbounded
    /// amount of child units if requested.
    /// </returns>
    public IAsyncEnumerable<OrganizationRecord> GetOrganizationByIdentifier(
        OrganizationIdentifier identifier,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single person (as a <see cref="IAsyncEnumerable{T}"/>).
    /// </summary>
    /// <param name="identifier">The person identifier.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing 0 or 1 <see cref="PersonRecord"/>.
    /// </returns>
    public IAsyncEnumerable<PersonRecord> GetPartyByPersonIdentifier(
        PersonIdentifier identifier,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up parties based on the provided identifiers. The returned parties
    /// is the result of a logical OR operation on the provided identifiers.
    /// </summary>
    /// <param name="partyUuids"><see cref="PartyRecord.PartyUuid"/>s.</param>
    /// <param name="partyIds"><see cref="PartyRecord.PartyId"/>s.</param>
    /// <param name="organizationIdentifiers"><see cref="PartyRecord.OrganizationIdentifier"/>s.</param>
    /// <param name="personIdentifiers"><see cref="PartyRecord.PersonIdentifier"/>s.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing the parties that match the provided identifiers,
    /// and optionally all their direct child units if requested.
    /// </returns>
    /// <remarks>
    /// If <paramref name="include"/> has the <see cref="PartyFieldIncludes.SubUnits"/> flag set,
    /// the result will contain all direct child units of the matched parties that are organizations
    /// and has child units. Any child unit will follow immediately after it's parent.
    /// </remarks>
    // TODO: https://github.com/npgsql/npgsql/issues/5655 - change to IReadOnlyList when Npgsql supports it
    public IAsyncEnumerable<PartyRecord> LookupParties(
        IList<Guid>? partyUuids = null,
        IList<int>? partyIds = null,
        IList<OrganizationIdentifier>? organizationIdentifiers = null,
        IList<PersonIdentifier>? personIdentifiers = null,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);
}
