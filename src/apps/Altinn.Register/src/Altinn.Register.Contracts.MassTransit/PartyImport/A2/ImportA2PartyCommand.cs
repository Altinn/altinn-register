using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit;

namespace Altinn.Register.Contracts.PartyImport.A2;

/// <summary>
/// A command for importing a party from A2.
/// </summary>
// Pinned to the original urn derived from the previous namespace
// (Altinn.Register.PartyImport.A2) so in-flight messages keep routing.
// MassTransit prepends the "urn:message:" prefix automatically.
[MessageUrn("Altinn.Register.PartyImport.A2:ImportA2PartyCommand")]
public sealed record ImportA2PartyCommand
    : CommandBase
{
    /// <summary>
    /// Gets the party UUID.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the change ID.
    /// </summary>
    public required uint ChangeId { get; init; }

    /// <summary>
    /// Gets when the change was registered.
    /// </summary>
    public required DateTimeOffset ChangedTime { get; init; }

    /// <summary>
    /// Gets tracking information for the import job.
    /// </summary>
    public UpsertPartyTracking Tracking { get; init; }
}
