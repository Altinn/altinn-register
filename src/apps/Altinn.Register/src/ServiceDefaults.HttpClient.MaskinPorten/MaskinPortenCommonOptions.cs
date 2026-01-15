namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Common settings used as defaults for all MaskinPorten clients.
/// </summary>
public sealed class MaskinPortenCommonOptions
{
    private static readonly Uri DefaultEndpoint = new("https://maskinporten.no/");

    private string? _audience;

    /// <summary>
    /// Gets or sets the base URL endpoint for the Maskinporten service.
    /// </summary>
    public Uri Endpoint { get; set; } = DefaultEndpoint;

    /// <summary>
    /// Gets or sets the the <c>audience</c> claim for the tokens.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Endpoint"/>.
    /// </remarks>
    public string Audience
    {
        get => _audience ?? Endpoint.OriginalString;
        set => _audience = value;
    }

    /// <summary>
    /// Gets or sets the duration for which the token remains valid.
    /// </summary>
    public TimeSpan TokenDuration { get; set; } = TimeSpan.FromMinutes(2);
}
