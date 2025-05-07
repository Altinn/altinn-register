#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.PartyImport;

/// <summary>
/// A command for upserting the user-info for a party.
/// </summary>
public sealed record UpsertPartyUserCommand
    : CommandBase
{
    /// <summary>
    /// Gets the party UUID.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the user record to upsert.
    /// </summary>
    public required PartyUserRecord User { get; init; }

    /// <summary>
    /// Gets the tracking information for the import.
    /// </summary>
    public UpsertPartyTracking Tracking { get; init; }
}
