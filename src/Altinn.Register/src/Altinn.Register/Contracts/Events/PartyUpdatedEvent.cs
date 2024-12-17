// TODO: This contract should be moved to a publishable package (keeping the same namespace).

#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit;

namespace Altinn.Register.Contracts.Events;

/// <summary>
/// An event that is published when a party is updated.
/// </summary>
[MessageUrn("event:altinn-register:party-updated")]
public sealed record PartyUpdatedEvent
    : EventBase
{
    /// <summary>
    /// The UUID of the party that was updated.
    /// </summary>
    public Guid PartyUuid { get; init; }
}
