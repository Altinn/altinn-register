namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Defines a contract for retrieving access tokens from Maskinporten.
/// </summary>
internal interface IMaskinPortenClient
{
    /// <summary>
    /// Retrieves an access token from Maskinporten using the specified client name.
    /// </summary>
    /// <param name="clientName">The name (configuration key) of the client.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>An access-token from MaskinPorten.</returns>
    public Task<MaskinPortenToken> GetAccessToken(string clientName, CancellationToken cancellationToken = default);
}
