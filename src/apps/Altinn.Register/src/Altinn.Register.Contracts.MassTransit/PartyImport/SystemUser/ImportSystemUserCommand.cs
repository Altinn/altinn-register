using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit;

namespace Altinn.Register.Contracts.PartyImport.SystemUser;

/// <summary>
/// A command sent for each system user that could not be imported.
/// </summary>
/// <remarks>
/// This exists to get retries and error handling.
/// </remarks>
// Pinned to the original urn derived from the previous namespace
// (Altinn.Register.PartyImport.SystemUser) so in-flight messages keep routing.
[MessageUrn("Altinn.Register.PartyImport.SystemUser:ImportSystemUserCommand")]
public sealed record ImportSystemUserCommand
    : CommandBase
{
    /// <summary>
    /// Gets the system user ID that could not be imported.
    /// </summary>
    public required Guid SystemUserId { get; init; }

    /// <summary>
    /// Gets the tracking information for the import.
    /// </summary>
    public required UpsertPartyTracking Tracking { get; init; }
}
