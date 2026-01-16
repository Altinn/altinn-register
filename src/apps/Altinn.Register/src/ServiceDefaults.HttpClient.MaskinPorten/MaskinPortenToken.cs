namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Represents an access token issued by Maskinporten, including its value and expiration time.
/// </summary>
public sealed record class MaskinPortenToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaskinPortenToken"/> class.
    /// </summary>
    /// <param name="cacheKey">The <see cref="MaskinPortenCacheKey"/>.</param>
    /// <param name="accessToken">The access token.</param>
    /// <param name="validTo">When the access token is valid to.</param>
    internal MaskinPortenToken(MaskinPortenCacheKey cacheKey, string accessToken, DateTimeOffset validTo)
    {
        CacheKey = cacheKey;
        AccessToken = accessToken;
        ValidTo = validTo;
    }

    /// <summary>
    /// Gets the client key associated with this token.
    /// </summary>
    internal MaskinPortenCacheKey CacheKey { get; }

    /// <summary>
    /// Gets the access token.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Gets when the token is valid to.
    /// </summary>
    public DateTimeOffset ValidTo { get; }
}
