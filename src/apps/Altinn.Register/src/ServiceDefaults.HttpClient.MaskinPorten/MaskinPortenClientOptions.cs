using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Settings for a MaskinPorten client.
/// </summary>
public sealed class MaskinPortenClientOptions
{
    /// <inheritdoc cref="MaskinPortenCommonOptions.Endpoint"/>
    [Required]
    public Uri? Endpoint { get; set; }

    /// <inheritdoc cref="MaskinPortenCommonOptions.TokenDuration"/>
    [Required]
    public TimeSpan? TokenDuration { get; set; }

    /// <summary>
    /// Gets or sets the <c>client_id</c> used when getting tokens.
    /// </summary>
    [Required]
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the <c>scope</c> used when getting tokens.
    /// </summary>
    [Required]
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the <c>resource</c> used when getting tokens (optional).
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Gets or sets the <c>consumer_org</c> used when getting tokens (optional).
    /// </summary>
    public string? ConsumerOrg { get; set; }

    /// <inheritdoc cref="MaskinPortenCommonOptions.Audience"/>
    [Required]
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets the JSON Web Key (JWK) used to authenticate with maskinporten.
    /// </summary>
    [Required]
    public JsonWebKey? Key { get; set; }
}
