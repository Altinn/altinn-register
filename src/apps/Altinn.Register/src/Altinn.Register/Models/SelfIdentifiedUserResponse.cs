using System.Text.Json.Serialization;
using Altinn.Register.Contracts;

namespace Altinn.Register.Models;

/// <summary>
/// Response body for the get-or-create self-identified user endpoint.
/// </summary>
public sealed class SelfIdentifiedUserResponse
{
    /// <summary>
    /// Gets or sets the party UUID.
    /// </summary>
    [JsonPropertyName("partyUuid")]
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets or sets the legacy numeric party id.
    /// </summary>
    [JsonPropertyName("partyId")]
    public required uint PartyId { get; init; }

    /// <summary>
    /// Gets or sets the legacy numeric user id.
    /// </summary>
    [JsonPropertyName("userId")]
    public required uint UserId { get; init; }

    /// <summary>
    /// Gets or sets the user's username (server-generated).
    /// </summary>
    [JsonPropertyName("userName")]
    public required string UserName { get; init; }

    /// <summary>
    /// Gets or sets the self-identified user type.
    /// </summary>
    [JsonPropertyName("selfIdentifiedUserType")]
    public required SelfIdentifiedUserType SelfIdentifiedUserType { get; init; }

    /// <summary>
    /// Gets or sets the canonical external URN for this user.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> for <see cref="SelfIdentifiedUserType.Educational"/>, which is stored
    /// in the register database with no <c>ext_urn</c>.
    /// </remarks>
    [JsonPropertyName("externalUrn")]
    public string? ExternalUrn { get; init; }
}
