#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.Parties;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// A command for importing user-ids from A2 for parties already imported into A3.
/// </summary>
public sealed record ImportA2UserIdForPartyCommand
    : CommandBase
{
    /// <summary>
    /// Gets the party UUID.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the <see cref="PartyType"/> of the party.
    /// </summary>
    public required PartyType PartyType { get; init; }

    /// <summary>
    /// Gets the tracking information for the import.
    /// </summary>
    public required UpsertPartyTracking Tracking { get; init; }
}
