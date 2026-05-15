using System.Text.Json.Serialization;

namespace Altinn.Register.Integrations.Sire.Organization;

/// <summary>
/// Represents a postal address from the SIRE API.
/// </summary>
public sealed record PostalAddress
{
    /// <summary>
    /// Gets the timestamp when the address was last updated.
    /// </summary>
    [JsonPropertyName("ajourholdstidspunkt")]
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the Norwegian address, if applicable.
    /// </summary>
    [JsonPropertyName("norskAdresse")]
    public NorwegianAddress? NorwegianAddress { get; init; }

    /// <summary>
    /// Gets the international address, if applicable.
    /// </summary>
    [JsonPropertyName("utenlandskAdresse")]
    public InternationalAddress? InternationalAddress { get; init; }
}
