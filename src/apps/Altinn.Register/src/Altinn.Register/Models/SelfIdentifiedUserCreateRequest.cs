using System.Text.Json.Serialization;
using Altinn.Register.Contracts;

namespace Altinn.Register.Models;

/// <summary>
/// Request body for the get-or-create self-identified user endpoint.
/// </summary>
/// <remarks>
/// Only <see cref="SelfIdentifiedUserType.Legacy"/> and <see cref="SelfIdentifiedUserType.IdPortenEmail"/>
/// are supported. The fields required depend on the declared type:
/// <list type="bullet">
///   <item><see cref="SelfIdentifiedUserType.IdPortenEmail"/> — <see cref="Email"/> is required.</item>
///   <item><see cref="SelfIdentifiedUserType.Legacy"/> — <see cref="Issuer"/> and <see cref="ExternalSubject"/> are required.</item>
/// </list>
/// </remarks>
public sealed class SelfIdentifiedUserCreateRequest
{
    /// <summary>
    /// Gets or sets the type of self-identified user to create.
    /// </summary>
    [JsonPropertyName("selfIdentifiedUserType")]
    public SelfIdentifiedUserType SelfIdentifiedUserType { get; set; }

    /// <summary>
    /// Gets or sets the user's email address (required when <see cref="SelfIdentifiedUserType"/>
    /// is <see cref="SelfIdentifiedUserType.IdPortenEmail"/>).
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the OIDC issuer (required when <see cref="SelfIdentifiedUserType"/>
    /// is <see cref="SelfIdentifiedUserType.Legacy"/>).
    /// </summary>
    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the OIDC subject (external identity) (required when <see cref="SelfIdentifiedUserType"/>
    /// is <see cref="SelfIdentifiedUserType.Legacy"/>).
    /// </summary>
    [JsonPropertyName("externalSubject")]
    public string? ExternalSubject { get; set; }

    /// <summary>
    /// Gets or sets an optional prefix for the generated username. Defaults to <c>altinn-</c> when omitted.
    /// </summary>
    [JsonPropertyName("userNamePrefix")]
    public string? UserNamePrefix { get; set; }
}
