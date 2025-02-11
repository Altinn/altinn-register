using System.Diagnostics.CodeAnalysis;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Persistence service for party roles.
/// </summary>
public interface IPartyExternalRolePersistence
{
    /// <summary>
    /// Gets all roles where <see cref="PartyExternalRoleAssignmentRecord.FromParty"/> is <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid"><see cref="PartyExternalRoleAssignmentRecord.FromParty"/>.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing all roles where <see cref="PartyExternalRoleAssignmentRecord.FromParty"/>
    /// is <paramref name="partyUuid"/>.
    /// </returns>
    public IAsyncEnumerable<PartyExternalRoleAssignmentRecord> GetExternalRoleAssignmentsFromParty(
        Guid partyUuid,
        PartyExternalRoleAssignmentFieldIncludes include = PartyExternalRoleAssignmentFieldIncludes.RoleAssignment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all roles where <see cref="PartyExternalRoleAssignmentRecord.ToParty"/> is <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid"><see cref="PartyExternalRoleAssignmentRecord.ToParty"/>.</param>
    /// <param name="include">Data/fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>
    /// A <see cref="IAsyncEnumerable{T}"/> containing all roles where <see cref="PartyExternalRoleAssignmentRecord.ToParty"/>
    /// is <paramref name="partyUuid"/>.
    /// </returns>
    public IAsyncEnumerable<PartyExternalRoleAssignmentRecord> GetExternalRoleAssignmentsToParty(
        Guid partyUuid,
        PartyExternalRoleAssignmentFieldIncludes include = PartyExternalRoleAssignmentFieldIncludes.RoleAssignment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts all external roles from <paramref name="partyUuid"/> by <paramref name="roleSource"/>.
    /// </summary>
    /// <param name="commandId">The command ID.</param>
    /// <param name="partyUuid">The party to upsert external roles from.</param>
    /// <param name="roleSource">The source of the external roles.</param>
    /// <param name="assignments">The new role-assignments.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns></returns>
    public IAsyncEnumerable<ExternalRoleAssignmentEvent> UpsertExternalRolesFromPartyBySource(
        Guid commandId,
        Guid partyUuid,
        ExternalRoleSource roleSource,
        IEnumerable<UpsertExternalRoleAssignment> assignments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Argument used in <see cref="UpsertExternalRolesFromPartyBySource(Guid, Guid, PartySource, IEnumerable{UpsertExternalRoleAssignment}, CancellationToken)"/>.
    /// </summary>
    public readonly record struct UpsertExternalRoleAssignment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpsertExternalRoleAssignment"/> record.
        /// </summary>
        /// <param name="roleIdentifier">The role identifier.</param>
        /// <param name="toParty">The receiving party.</param>
        [SetsRequiredMembers]
        public UpsertExternalRoleAssignment(string roleIdentifier, Guid toParty)
        {
            RoleIdentifier = roleIdentifier;
            ToParty = toParty;
        }

        /// <summary>
        /// Gets the role identifier.
        /// </summary>
        public readonly string RoleIdentifier { get; init; }

        /// <summary>
        /// Gets the receiving party.
        /// </summary>
        public readonly Guid ToParty { get; init; }
    }
}
