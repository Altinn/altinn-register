using System.Text.Json.Serialization;
using MassTransit;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// Base record for a mass transit event.
/// </summary>
[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public abstract record EventBase
    : CorrelatedBy<Guid>
{
    private readonly Guid _id = Guid.CreateVersion7();

    /// <summary>
    /// Gets the event ID.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("eventId")]
    public Guid EventId
    {
        get => _id;
        private init => _id = value;
    }

    /// <inheritdoc />
    Guid CorrelatedBy<Guid>.CorrelationId => EventId;
}
