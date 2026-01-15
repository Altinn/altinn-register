using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Altinn.Authorization.ServiceDefaults.Telemetry;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// A client for getting access-tokens from MaskinPorten.
/// </summary>
internal sealed partial class MaskinPortenClient
    : IMaskinPortenClient
{
    /// <summary>
    /// The name of the HTTP client used for Maskinporten authentication requests.
    /// </summary>
    internal static readonly string MaskinPortenHttpClientName = $"{nameof(ServiceDefaults)}.{nameof(MaskinPortenClient)}";

    private readonly AsyncMultiLock _lock = new();
    private readonly ConcurrentDictionary<string, CacheKeyStore> _cacheKeys = new();
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MaskinPortenClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<MaskinPortenClientOptions> _options;

    /// <summary>
    /// Initializes a new <see cref="MaskinPortenClient"/>.
    /// </summary>
    public MaskinPortenClient(
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<MaskinPortenClient> logger,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<MaskinPortenClientOptions> options)
    {
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    /// <inheritdoc/>
    /// <remarks>If a valid token is available in the cache for the specified key, it is returned immediately;
    /// otherwise, a new token is requested from Maskinporten and cached for subsequent calls. This method is
    /// thread-safe.</remarks>
    public async Task<MaskinPortenToken> GetAccessToken(string clientName, CancellationToken cancellationToken = default)
    {
        var settings = _options.Get(clientName);
        var key = GetCacheKey(clientName, settings);

        using var activity = MaskinPortenClientTelemetry.Source.StartActivity(
            ActivityKind.Internal,
            "get maskinporten token",
            tags: [
                new("maskin_porten.client_name", clientName),
                new("maskin_porten.client_id", key.ClientId),
                new("maskin_porten.scope", key.Scope),
                new("maskin_porten.resource", key.Resource),
                new("maskin_porten.consumer_org", key.ConsumerOrg),
            ]);

        try
        {
            if (TryGetToken(key, out var token))
            {
                activity?.AddTag("cache.hit", "true");
                activity?.AddTag("cache.lock_acquired", "false");
                return token;
            }

            return await FetchAndCacheToken(key, settings, activity, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<MaskinPortenToken> FetchAndCacheToken(MaskinPortenCacheKey key, MaskinPortenClientOptions settings, Activity? activity, CancellationToken cancellationToken)
    {
        using var ticket = await _lock.Acquire(key, cancellationToken);
        activity?.AddTag("cache.lock_acquired", "true");

        if (TryGetToken(key, out var token))
        {
            activity?.AddTag("cache.hit", "true");
            return token;
        }

        activity?.AddTag("cache.hit", "false");
        using ICacheEntry entry = _cache.CreateEntry(key);

        token = await FetchToken(key, settings, activity, cancellationToken);
        activity?.SetStatus(ActivityStatusCode.Ok);

        entry.AbsoluteExpiration = token.ValidTo;
        entry.Value = token;
        return token;
    }

    private async Task<MaskinPortenToken> FetchToken(MaskinPortenCacheKey key, MaskinPortenClientOptions settings, Activity? activity, CancellationToken cancellationToken)
    {
        Log.FetchToken(_logger, key);

        using var client = _httpClientFactory.CreateClient(MaskinPortenHttpClientName);

        var jwtAssertion = GetJwtAssertion(key, settings);

        HttpStatusCode? statusCode = null;
        ErrorReponse? errorResponse = null;
        Exception? inner = null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(settings.Endpoint!, "token"));
            req.Content = new FormUrlEncodedContent([
                new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new("assertion", jwtAssertion),
            ]);

            using var res = await client.SendAsync(req, cancellationToken);
            statusCode = res.StatusCode;
            activity?.SetTag("response.status_code", (int)statusCode);

            if (res.IsSuccessStatusCode)
            {
                var tokenResponse = await res.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
                Assert(tokenResponse is not null, "Token-response should never be null");
                Assert(!string.IsNullOrEmpty(tokenResponse.AccessToken), "Token-response missing access-token");
                Assert(tokenResponse.ExpiresIn is not null, "Token-response missing expires-in");
                Assert(tokenResponse.ExpiresIn is > 0, "Token-response expires-in less than or equal to 0");

                var now = _timeProvider.GetUtcNow();
                var validTo = now + TimeSpan.FromSeconds(tokenResponse.ExpiresIn!.Value) - TimeSpan.FromSeconds(5);
                return new MaskinPortenToken(key, tokenResponse.AccessToken!, validTo);
            }

            errorResponse = await res.Content.ReadFromJsonAsync<ErrorReponse>(cancellationToken);

            if (errorResponse is not null)
            {
                activity?.SetTag("response.error", errorResponse.Error);
                activity?.SetTag("response.error_description", errorResponse.Description);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            inner = e;
        }

        Log.FetchFailed(_logger, key, statusCode, inner);
        throw new TokenRequestException(
            key,
            inner,
            statusCode,
            errorResponse is null ? null : (errorResponse.Error, errorResponse.Description));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
            {
                ThrowHelper.ThrowInvalidOperationException(message);
            }
        }
    }

    private bool TryGetToken(MaskinPortenCacheKey key, [NotNullWhen(true)] out MaskinPortenToken? result)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (_cache.TryGetValue(key, out MaskinPortenToken? token)
            && token is { ValidTo: var validTo }
            && validTo > now)
        {
            Debug.Assert(ReferenceEquals(key, token.CacheKey));
            result = token;
            return true;
        }

        result = null;
        return false;
    }

    private string GetJwtAssertion(MaskinPortenCacheKey key, MaskinPortenClientOptions settings)
    {
        var now = _timeProvider.GetUtcNow();
        var header = new JwtHeader(new SigningCredentials(settings.Key, settings.Key!.Alg));
        var payload = new JwtPayload
        {
            { "aud", settings.Audience },
            { "scope", key.Scope },
            { "iss", key.ClientId },
            { "exp", (now + settings.TokenDuration!.Value).ToUnixTimeSeconds() },
            { "iat", now.ToUnixTimeSeconds() },
            { "jti", Guid.NewGuid().ToString() },
        };

        if (!string.IsNullOrEmpty(key.Resource))
        {
            payload.Add("resource", key.Resource);
        }

        if (!string.IsNullOrEmpty(key.ConsumerOrg))
        {
            payload.Add("consumer_org", key.ConsumerOrg);
        }

        var securityToken = new JwtSecurityToken(header, payload);
        var handler = new JwtSecurityTokenHandler();

        return handler.WriteToken(securityToken);
    }

    private MaskinPortenCacheKey GetCacheKey(string clientName, MaskinPortenClientOptions settings)
    {
        var store = _cacheKeys.GetOrAdd(clientName, static name => new CacheKeyStore(name));
        return store.Get(settings);
    }

    private sealed class AsyncMultiLock
    {
        private readonly ConcurrentDictionary<MaskinPortenCacheKey, SemaphoreSlim> _locks = new();

        public async ValueTask<IDisposable> Acquire(MaskinPortenCacheKey key, CancellationToken cancellationToken)
        {
            var semaphore = _locks.GetOrAdd(key, static _ => new(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            return new LockGuard(semaphore);
        }

        private sealed class LockGuard
            : IDisposable
        {
            private SemaphoreSlim? _semaphore;

            public LockGuard(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _semaphore, null) is { } semaphore)
                {
                    semaphore.Release();
                }
            }
        }
    }

    private sealed class CacheKeyStore(string clientName)
    {
        private MaskinPortenCacheKey? _key;

        public MaskinPortenCacheKey Get(MaskinPortenClientOptions options)
        {
            while (true)
            {
                var key = Volatile.Read(ref _key);
                if (Matches(key, options))
                {
                    return key;
                }

                var newKey = new MaskinPortenCacheKey(
                    clientName,
                    options.ClientId!,
                    options.Scope!,
                    options.Resource,
                    options.ConsumerOrg);

                if (ReferenceEquals(Interlocked.CompareExchange(ref _key, newKey, key), key))
                {
                    return newKey;
                }
            }

            static bool Matches([NotNullWhen(true)] MaskinPortenCacheKey? key, MaskinPortenClientOptions options)
            {
                if (key is null)
                {
                    return false;
                }

                return key.ClientId == options.ClientId
                    && key.Scope == options.Scope
                    && key.Resource == options.Resource
                    && key.ConsumerOrg == options.ConsumerOrg;
            }
        }
    }

    /// <summary>
    /// The TokenResponse from Maskinporten
    /// </summary>
    private sealed record class TokenResponse
    {
        /// <summary>
        /// An Oauth2 access token, either by reference or as a JWT depending on which scopes was requested and/or client registration properties.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        /// <summary>
        /// Number of seconds until this access_token is no longer valid
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }

        /// <summary>
        /// The list of scopes issued in the access token. Included for convenience only, and should not be trusted for access control decisions.
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; init; }

        /// <summary>
        /// Type of token
        /// </summary>
        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }

    /// <summary>
    /// An error from Maskinporten
    /// </summary>
    private sealed record class ErrorReponse
    {
        /// <summary>
        /// The type of error
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Description of the error
        /// </summary>
        [JsonPropertyName("error_description")]
        public string? Description { get; set; }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Fetch Maskinporten token for ClientId: '{clientId}', Scope: '{scope}', Resource: '{resource}', ConsumerOrg: '{consumerOrg}'.")]
        private static partial void FetchToken(ILogger logger, string clientId, string scope, string? resource, string? consumerOrg);
        
        public static void FetchToken(ILogger logger, MaskinPortenCacheKey key)
            => FetchToken(logger, key.ClientId, key.Scope, key.Resource, key.ConsumerOrg);

        [LoggerMessage(2, LogLevel.Error, "Failed to fetch Maskinporten token for ClientId: '{clientId}', Scope: '{scope}', Resource: '{resource}', ConsumerOrg: '{consumerOrg}'. StatusCode: '{statusCode}'.")]
        private static partial void FetchFailed(ILogger logger, string clientId, string scope, string? resource, string? consumerOrg, HttpStatusCode? statusCode, Exception? inner);

        public static void FetchFailed(ILogger logger, MaskinPortenCacheKey key, HttpStatusCode? statusCode, Exception? inner)
            => FetchFailed(logger, key.ClientId, key.Scope, key.Resource, key.ConsumerOrg, statusCode, inner);
    }
}
