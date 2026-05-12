using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit;

namespace Altinn.Register.Contracts.PartyImport.A2;

/// <summary>
/// A command for importing an A2 user profile.
/// </summary>
// Pinned to the original urn derived from the previous namespace
// (Altinn.Register.PartyImport.A2) so in-flight messages keep routing.
// MassTransit prepends the "urn:message:" prefix automatically.
[MessageUrn("Altinn.Register.PartyImport.A2:ImportA2UserProfileCommand")]
public sealed record ImportA2UserProfileCommand
    : CommandBase
{
    /// <summary>
    /// Gets the user ID.
    /// </summary>
    public required ulong UserId { get; init; }

    /// <summary>
    /// Gets the party UUID.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the owner party UUID.
    /// </summary>
    public required Guid OwnerPartyUuid { get; init; }

    /// <summary>
    /// Gets whether the user profile was deleted at the update time.
    /// </summary>
    public required bool IsDeleted { get; init; }

    /// <summary>
    /// Gets tracking information for the import job.
    /// </summary>
    public required UpsertPartyTracking Tracking { get; init; }
}
