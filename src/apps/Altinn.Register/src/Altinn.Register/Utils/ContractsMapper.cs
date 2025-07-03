using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Utils;

/// <summary>
/// Utility class for mapping models to contracts.
/// </summary>
internal static class ContractsMapper
{
    /// <summary>
    /// Maps a party uuid to a party reference contract.
    /// </summary>
    /// <param name="partyUuid">The party UUID.</param>
    /// <returns>A <see cref="PartyReference"/>.</returns>
    public static PartyReference ToPartyReferenceContract(this Guid partyUuid)
        => new(partyUuid);

    /// <summary>
    /// Maps a <see cref="ExternalRoleAssignmentEvent"/> to a <see cref="ExternalRoleReference"/>.
    /// </summary>
    /// <param name="evt">The external role assignment event.</param>
    /// <returns>A <see cref="ExternalRoleReference"/>.</returns>
    public static ExternalRoleReference ToPartyExternalRoleReferenceContract(this ExternalRoleAssignmentEvent evt)
        => new(evt.RoleSource, evt.RoleIdentifier);
}
