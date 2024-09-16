using System.Text.Json.Serialization;

namespace Altinn.Platform.Register.Models;

/// <summary>
/// Represents the party lookup result
/// </summary>
public record PartyName
{
    /// <summary>
    /// Gets or sets the social security number for this result.
    /// </summary>
    [JsonPropertyName("ssn")]
    public string? Ssn { get; set; }

    /// <summary>
    /// Gets or sets the organization number for this result.
    /// </summary>
    [JsonPropertyName("orgNo")]
    public string? OrgNo { get; set; }

    /// <summary>
    /// Gets or sets the party name for this result.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the first name for this result.
    /// </summary>
    [JsonPropertyName("firstName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the middle name for this result.
    /// </summary>
    [JsonPropertyName("middleName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MiddleName { get; set; }

    /// <summary>
    /// Gets or sets the sure name for this result.
    /// </summary>
    [JsonPropertyName("lastName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastName { get; set; }
}
