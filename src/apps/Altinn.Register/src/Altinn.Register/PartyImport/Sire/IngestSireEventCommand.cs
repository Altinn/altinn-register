using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts;

namespace Altinn.Register.PartyImport.Sire;

/// <summary>
/// Command for ingesting a SIRE event. Published by the SIRE events polling job.
/// </summary>
public sealed record IngestSireEventCommand
    : CommandBase
{
    /// <summary>
    /// Gets the sequence number of the event.
    /// </summary>
    public required uint SequenceNumber { get; init; }

    /// <summary>
    /// Gets the 9-digit organization identifier.
    /// </summary>
    public required OrganizationIdentifier Identifier { get; init; }

    /// <summary>
    /// Gets the event type (hendelsetype) from the SIRE events API.
    /// </summary>
    public required string EventType { get; init; }
}
