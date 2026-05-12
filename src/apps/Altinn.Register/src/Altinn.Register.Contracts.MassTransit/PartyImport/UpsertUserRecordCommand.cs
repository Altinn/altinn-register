using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit;

namespace Altinn.Register.Contracts.PartyImport;

/// <summary>
/// A command for upserting a user record.
/// </summary>
// Pinned to the original urn derived from the previous namespace
// (Altinn.Register.PartyImport) so in-flight messages keep routing.
[MessageUrn("Altinn.Register.PartyImport:UpsertUserRecordCommand")]
public sealed record UpsertUserRecordCommand
    : CommandBase
{
    /// <summary>
    /// The owner party UUID.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the user ID.
    /// </summary>
    public required ulong UserId { get; init; }

    /// <summary>
    /// Gets the username, if any.
    /// </summary>
    public required string? Username { get; init; }

    /// <summary>
    /// Gets whether the user is active.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Gets the tracking information for the import.
    /// </summary>
    public UpsertPartyTracking Tracking { get; init; }
}
