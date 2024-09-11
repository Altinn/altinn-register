using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Persistence service for party roles.
/// </summary>
public interface IPartyRolePersistence
{
    /// <summary>
    /// Gets all roles where <see cref="PartyRoleRecord.FromParty"/> is <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid"><see cref="PartyRoleRecord.FromParty"/>.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing all roles where <see cref="PartyRoleRecord.FromParty"/>
    /// is <paramref name="partyUuid"/>.
    /// </returns>
    public IAsyncEnumerable<PartyRoleRecord> GetRolesFromParty(
        Guid partyUuid,
        PartyRoleFieldIncludes include = PartyRoleFieldIncludes.Role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all roles where <see cref="PartyRoleRecord.ToParty"/> is <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid"><see cref="PartyRoleRecord.ToParty"/>.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing all roles where <see cref="PartyRoleRecord.ToParty"/>
    /// is <paramref name="partyUuid"/>.
    /// </returns>
    public IAsyncEnumerable<PartyRoleRecord> GetRolesToParty(
        Guid partyUuid,
        PartyRoleFieldIncludes include = PartyRoleFieldIncludes.Role,
        CancellationToken cancellationToken = default);
}
