#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.PartyImport.A2;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// A command for resolving external role assignments fetched from A2
/// and upserting them.
/// </summary>
public sealed record ResolveAndUpsertA2CCRRoleAssignmentsCommand
    : CommandBase
{
    /// <summary>
    /// Gets the party UUID that the role assignments are from.
    /// </summary>
    public required Guid FromPartyUuid { get; init; }

    /// <summary>
    /// Gets the role assignments.
    /// </summary>
    public required IReadOnlyList<A2PartyExternalRoleAssignment> RoleAssignments { get; init; }

    /// <summary>
    /// Gets the tracking information for the import.
    /// </summary>
    public required UpsertPartyTracking Tracking { get; init; }
}
