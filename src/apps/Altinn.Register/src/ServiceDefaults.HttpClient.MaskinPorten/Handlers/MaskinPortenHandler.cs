namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Handlers;

/// <summary>
/// A message handler that automatically acquires and attaches Maskinporten access tokens to outgoing HTTP requests
/// based on per-request configuration.
/// </summary>
/// <remarks>MaskinPortenHandler inspects each HTTP request for a configured Maskinporten client key. If present,
/// it ensures a valid Maskinporten access token is attached as a Bearer token in the Authorization header. If a valid
/// token is already present and matches the configured client key, it is reused; otherwise, a new token is acquired.
/// This handler is intended for use in HTTP pipelines where Maskinporten authentication is required on a per-request
/// basis. Requests without a configured client key are passed through without modification. This handler should be
/// added after any retry handlers.</remarks>
internal sealed class MaskinPortenHandler
    : AsyncOnlyDelegatingHandler
{
    private static readonly HttpRequestOptionsKey<MaskinPortenToken> KeyMaskinPortenToken = new($"{nameof(ServiceDefaults)}.{nameof(MaskinPortenToken)}");

    private readonly TimeProvider _timeProvider;
    private readonly IMaskinPortenClient _client;

    /// <summary>
    /// Initializes a new <see cref="MaskinPortenHandler"/>.
    /// </summary>
    internal MaskinPortenHandler(TimeProvider timeProvider, IMaskinPortenClient client)
    {
        _timeProvider = timeProvider;
        _client = client;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Options.TryGetMaskinPortenClientName(out var clientName))
        {
            // no MaskinPorten client-key configured, so we do not run on this request
            return base.SendAsync(request, cancellationToken);
        }

        if (request.Headers.Authorization is { Scheme: string scheme, Parameter: string token })
        {
            if (!string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                // Existing non-bearer token, do not modify
                return base.SendAsync(request, cancellationToken);
            }

            return HandleExistingToken(request, clientName, token, cancellationToken);
        }

        // fetch a new token and send the request
        return AddToken(request, clientName, cancellationToken);
    }

    private Task<HttpResponseMessage> HandleExistingToken(HttpRequestMessage request, string clientName, string token, CancellationToken cancellationToken)
    {
        if (!request.Options.TryGetValue(KeyMaskinPortenToken, out var tokenObj)
            || tokenObj.AccessToken != token)
        {
            // token is added manually/not a MaskinPorten token, so we leave it alone
            return base.SendAsync(request, cancellationToken);
        }

        if (!string.Equals(clientName, tokenObj.CacheKey.ClientName, StringComparison.Ordinal))
        {
            // Token belongs to a different key - this should not happen
            throw new InvalidOperationException($"client-id and cache-key.client-id configured in request does not match");
        }

        var now = _timeProvider.GetUtcNow();
        if (tokenObj.ValidTo > now)
        {
            // token is still valid, so just send it
            return base.SendAsync(request, cancellationToken);
        }

        // token is expired - this can happen if there are retries in the pipeline for instance
        // fetch a new token and send the request
        return AddToken(request, clientName, cancellationToken);
    }

    private async Task<HttpResponseMessage> AddToken(HttpRequestMessage request, string clientName, CancellationToken cancellationToken)
    {
        var token = await _client.GetAccessToken(clientName, cancellationToken);
        request.Options.Set(KeyMaskinPortenToken, token);
        request.Headers.Authorization = new("Bearer", token.AccessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
