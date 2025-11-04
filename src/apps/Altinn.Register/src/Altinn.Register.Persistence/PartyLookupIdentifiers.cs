using System.Text.Json.Serialization;

namespace Altinn.Register.Persistence;

/// <summary>
/// Represents the identifiers that can be used to look up parties.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PartyLookupIdentifiers
    : byte
{
    /// <summary>
    /// Do not lookup based on anything (effectively, get all).
    /// </summary>
    [JsonStringEnumMemberName("none")]
    None = 0,

    /// <summary>
    /// Get by party id.
    /// </summary>
    [JsonStringEnumMemberName("id")]
    PartyId = 1 << 1,

    /// <summary>
    /// Get by party UUID.
    /// </summary>
    [JsonStringEnumMemberName("uuid")]
    PartyUuid = 1 << 2,

    /// <summary>
    /// Get by person identifier.
    /// </summary>
    [JsonStringEnumMemberName("pers.id")]
    PersonIdentifier = 1 << 3,

    /// <summary>
    /// Get by organization identifier.
    /// </summary>
    [JsonStringEnumMemberName("org.id")]
    OrganizationIdentifier = 1 << 4,

    /// <summary>
    /// Get by user id.
    /// </summary>
    [JsonStringEnumMemberName("user.id")]
    UserId = 1 << 5,

    /// <summary>
    /// Get by username.
    /// </summary>
    [JsonStringEnumMemberName("user.name")]
    Username = 1 << 6,
}
