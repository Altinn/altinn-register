using System.Text.Json.Serialization;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Represents a party type.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PartyType>))]
public enum PartyType
{
    /// <summary>
    /// Person party type.
    /// </summary>
    [JsonStringEnumMemberName("person")]
    Person,

    /// <summary>
    /// Organization party type.
    /// </summary>
    [JsonStringEnumMemberName("organization")]
    Organization,

    /// <summary>
    /// Self-identified user party type.
    /// </summary>
    [JsonStringEnumMemberName("self-identified-user")]
    SelfIdentifiedUser,
}
