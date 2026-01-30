namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Represents an access token issued by Maskinporten, including its value and expiration time.
/// </summary>
public sealed record class MaskinPortenToken
{
    /// <summary>
    /// Creates a new instance of the MaskinPortenToken class using the specified client and token information.
    /// </summary>
    /// <remarks>This method is only intended for testing.</remarks>
    /// <param name="clientName">The name of the client application requesting the token. Cannot be null or empty.</param>
    /// <param name="clientId">The unique identifier of the client application. Cannot be null or empty.</param>
    /// <param name="scope">The scope for which the token is issued. Cannot be null or empty.</param>
    /// <param name="resource">The resource identifier for which the token is intended, or null if not applicable.</param>
    /// <param name="consumerOrg">The organization number of the consumer, or null if not applicable.</param>
    /// <param name="accessToken">The access token string to associate with the MaskinPortenToken. Cannot be null or empty.</param>
    /// <param name="validTo">The date and time when the token expires, expressed as a DateTimeOffset.</param>
    /// <returns>A new MaskinPortenToken instance containing the specified client and token details.</returns>
    public static MaskinPortenToken Create(
        string clientName,
        string clientId,
        string scope,
        string? resource,
        string? consumerOrg,
        string accessToken,
        DateTimeOffset validTo)
    {
        var cacheKey = new MaskinPortenCacheKey(clientName, clientId, scope, resource, consumerOrg);
        return new MaskinPortenToken(cacheKey, accessToken, validTo);
    }

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
