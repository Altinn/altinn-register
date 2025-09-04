namespace Altinn.Register.Core.PartyImport.A2;

/// <summary>
/// Represents an external role assignment from a party.
/// </summary>
public sealed record A2PartyExternalRoleAssignment
{
    /// <summary>
    /// Gets the party uuid of the receiving party.
    /// </summary>
    public required Guid ToPartyUuid { get; init; }

    /// <summary>
    /// Gets the role code.
    /// </summary>
    public required string RoleCode { get; init; }
}
