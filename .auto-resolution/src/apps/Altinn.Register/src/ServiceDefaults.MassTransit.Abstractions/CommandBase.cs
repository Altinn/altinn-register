using System.Text.Json.Serialization;
using MassTransit;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// Base record for a mass transit command.
/// </summary>
[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public abstract record CommandBase
    : CorrelatedBy<Guid>
{
    private readonly Guid _id = Guid.CreateVersion7();

    /// <summary>
    /// Gets the command ID.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("commandId")]
    public Guid CommandId
    {
        get => _id;
        private init => _id = value;
    }

    /// <inheritdoc />
    Guid CorrelatedBy<Guid>.CorrelationId => CommandId;
}
