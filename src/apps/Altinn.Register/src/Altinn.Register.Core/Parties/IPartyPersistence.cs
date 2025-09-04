using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;
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
        uint partyId,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to upsert a party.
    /// </summary>
    /// <param name="party">The party to upsert.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The updated party.</returns>
    public Task<Result<PartyRecord>> UpsertParty(
        PartyRecord party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to upsert multiple party.
    /// </summary>
    /// <param name="parties">The parties to upsert.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The updated party.</returns>
    public IAsyncEnumerable<Result<PartyRecord>> UpsertParties(
        IAsyncEnumerable<PartyRecord> parties,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to upsert multiple party.
    /// </summary>
    /// <param name="parties">The parties to upsert.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The updated party.</returns>
    public IAsyncEnumerable<Result<PartyRecord>> UpsertParties(
        IEnumerable<PartyRecord> parties,
        CancellationToken cancellationToken = default)
        => UpsertParties(parties.ToAsyncEnumerable(), cancellationToken);

    /// <summary>
    /// Attempts to upsert a party user.
    /// </summary>
    /// <param name="partyUuid">The party uuid.</param>
    /// <param name="user">The <see cref="PartyUserRecord"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The updated party user.</returns>
    public Task<Result<PartyUserRecord>> UpsertPartyUser(
        Guid partyUuid,
        PartyUserRecord user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to upsert a party user record based on userId.
    /// </summary>
    /// <param name="partyUuid">The party UUID.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="username">The username.</param>
    /// <param name="isActive">Whether or not the user is active.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns></returns>
    public Task<Result> UpsertUserRecord(
        Guid partyUuid,
        ulong userId,
        FieldValue<string> username,
        bool isActive,
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
    public IAsyncEnumerable<PersonRecord> GetPersonByIdentifier(
        PersonIdentifier identifier,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single party (as a <see cref="IAsyncEnumerable{T}"/>).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing 0 or 1 <see cref="PartyRecord"/>.
    /// </returns>
    public IAsyncEnumerable<PartyRecord> GetPartyByUserId(
        uint userId,
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
    /// <param name="userIds"><see cref="PartyRecord.User"/>'s <see cref="PartyUserRecord.UserIds"/>.</param>
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
        IReadOnlyList<Guid>? partyUuids = null,
        IReadOnlyList<uint>? partyIds = null,
        IReadOnlyList<OrganizationIdentifier>? organizationIdentifiers = null,
        IReadOnlyList<PersonIdentifier>? personIdentifiers = null,
        IReadOnlyList<uint>? userIds = null,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of the parties-stream.
    /// </summary>
    /// <param name="fromExclusive">From where (exclusive) to start the page.</param>
    /// <param name="limit">The maximum number of parties to return.</param>
    /// <param name="include">What fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing a page of parties.
    /// </returns>
    /// <remarks>
    /// <paramref name="include"/> is not allowed to specify <see cref="PartyFieldIncludes.SubUnits"/>.
    /// </remarks>
    public IAsyncEnumerable<PartyRecord> GetPartyStream(
        ulong fromExclusive,
        ushort limit,
        PartyFieldIncludes include = PartyFieldIncludes.Party,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of the external-role-assignment-stream.
    /// </summary>
    /// <param name="fromExclusive">From where (exclusive) to start the page.</param>
    /// <param name="limit">The maximum number of parties to return.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing a page of external-role-assignment events.
    /// </returns>
    public IAsyncEnumerable<ExternalRoleAssignmentEvent> GetExternalRoleAssignmentStream(
        ulong fromExclusive,
        ushort limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the maximum version ID of a party.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The highest version ID of a party.</returns>
    public Task<ulong> GetMaxPartyVersionId(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the maximum version ID of an external-role-assignment event.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The highest version ID of an external-role-assignment event.</returns>
    public Task<ulong> GetMaxExternalRoleAssignmentVersionId(CancellationToken cancellationToken);
}
