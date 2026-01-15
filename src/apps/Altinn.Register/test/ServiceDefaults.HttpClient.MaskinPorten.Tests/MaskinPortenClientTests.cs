using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Fakes;
using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Utils;
using Altinn.Authorization.TestUtils.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests;

public class MaskinPortenClientTests
    : IAsyncDisposable
{
    private static readonly JsonWebKey PrivateKey = JsonWebKey.Create(
        """
        {
            "kty": "EC",
            "d": "NPt0oOMxgXa3ygpGEWdgsATEexWowIjTKFavPyYGgxE",
            "use": "sig",
            "crv": "P-256",
            "kid": "test-key",
            "x": "SorMtDy8raOt-mBpKK3REMVB_gR4wgnNrhYBom3qQRA",
            "y": "4UlRyg2BXk1zJmc1x0CyDQiHZbSFPnH_O0ML38mEZqk",
            "alg": "ES256"
        }
        """);

    private static readonly JsonWebKey PublicKey = JsonWebKey.Create(
        """
        {
            "kty": "EC",
            "use": "sig",
            "crv": "P-256",
            "kid": "test-key",
            "x": "SorMtDy8raOt-mBpKK3REMVB_gR4wgnNrhYBom3qQRA",
            "y": "4UlRyg2BXk1zJmc1x0CyDQiHZbSFPnH_O0ML38mEZqk",
            "alg": "ES256"
        }
        """);

    private readonly IMemoryCache _cache;
    private readonly FakeTimeProvider _timeProvider;
    private readonly FakeHttpClientFactory _httpClientFactory;
    private readonly TestOptionsMonitor _options;
    private readonly MaskinPortenClient _sut;

    private FakeHttpMessageHandler Handler => _httpClientFactory.For(MaskinPortenClient.MaskinPortenHttpClientName);

    public MaskinPortenClientTests()
    {
        _timeProvider = new();
        _cache = new MemoryCache(new MemoryCacheOptions { Clock = new FakeClock(_timeProvider) });
        _httpClientFactory = new();
        _options = new();

        _sut = new MaskinPortenClient(
            cache: _cache,
            timeProvider: _timeProvider,
            logger: new NullLogger<MaskinPortenClient>(),
            httpClientFactory: _httpClientFactory,
            options: _options);
    }

    [Fact]
    public async Task GetAccessToken_TokenEndpointResponseError_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(HttpStatusCode.ServiceUnavailable);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetAccessToken_Cancelled_Throws()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(() =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();

                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); // unreachable
            });

        var exn = await Should.ThrowAsync<OperationCanceledException>(() => _sut.GetAccessToken("test", cts.Token));
        exn.CancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task GetAccessToken_ResponseNull_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond("application/json", "null");

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response should never be null");
    }

    [Fact]
    public async Task GetAccessToken_ResponseMissingAccessToken_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(
                "application/json", 
                """
                {}
                """);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response missing access-token");
    }

    [Fact]
    public async Task GetAccessToken_ResponseNullAccessToken_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(
                "application/json",
                """
                {
                    "access_token": null
                }
                """);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response missing access-token");
    }

    [Fact]
    public async Task GetAccessToken_ResponseEmptyAccessToken_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(
                "application/json",
                """
                {
                    "access_token": ""
                }
                """);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response missing access-token");
    }

    [Fact]
    public async Task GetAccessToken_ResponseMissingExpiry_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(
                "application/json",
                """
                {
                    "access_token": "token"
                }
                """);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response missing expires-in");
    }

    [Fact]
    public async Task GetAccessToken_ResponseNullExpiry_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(
                "application/json",
                """
                {
                    "access_token": "token",
                    "expires_in": null
                }
                """);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response missing expires-in");
    }

    [Fact]
    public async Task GetAccessToken_ResponseExpiryZero_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(
                "application/json",
                """
                {
                    "access_token": "token",
                    "expires_in": 0
                }
                """);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response expires-in less than or equal to 0");
    }

    [Fact]
    public async Task GetAccessToken_ResponseExpiryNegative_Throws()
    {
        Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(
                "application/json",
                """
                {
                    "access_token": "token",
                    "expires_in": -20
                }
                """);

        var exn = await Should.ThrowAsync<TokenRequestException>(() => _sut.GetAccessToken("test", TestContext.Current.CancellationToken));
        exn.StatusCode.ShouldBe(HttpStatusCode.OK);
        exn.InnerException.ShouldNotBeNull();
        exn.InnerException.Message.ShouldBe("Token-response expires-in less than or equal to 0");
    }

    [Fact]
    public async Task GetAccessToken_FetchesNewToken()
    {
        var options = Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(CreateToken(options));

        var token = await _sut.GetAccessToken("test", TestContext.Current.CancellationToken);

        var now = _timeProvider.GetUtcNow();
        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe($"{options.ClientId!}:{options.Scope}:{_timeProvider.GetUtcNow()}");
        token.ValidTo.ShouldBeLessThan(now + options.TokenDuration!.Value);
        token.ValidTo.ShouldBeGreaterThan(now);
    }

    [Fact]
    public async Task GetAccessToken_IsCached()
    {
        var options = Configure("test");
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(CreateToken(options));

        var token1 = await _sut.GetAccessToken("test", TestContext.Current.CancellationToken);
        var token2 = await _sut.GetAccessToken("test", TestContext.Current.CancellationToken);

        token2.AccessToken.ShouldBe(token1.AccessToken);
    }

    [Fact]
    public async Task GetAccessToken_CacheIsNotSharedBetweenClients()
    {
        var options1 = Configure("test1");
        var options2 = Configure("test2");

        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(CreateToken(options1));

        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(CreateToken(options2));

        var token1 = await _sut.GetAccessToken("test1", TestContext.Current.CancellationToken);
        var token2 = await _sut.GetAccessToken("test2", TestContext.Current.CancellationToken);

        token2.AccessToken.ShouldNotBe(token1.AccessToken);
    }

    [Fact]
    public async Task GetAccessToken_IsRefetched_WhenExpired()
    {
        var options = Configure("test");
        
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(CreateToken(options));
        
        Handler.Expect(HttpMethod.Post, "/token")
            .Respond(CreateToken(options));

        var token1 = await _sut.GetAccessToken("test", TestContext.Current.CancellationToken);

        _timeProvider.Advance(TimeSpan.FromHours(1));

        var token2 = await _sut.GetAccessToken("test", TestContext.Current.CancellationToken);

        token2.AccessToken.ShouldNotBe(token1.AccessToken);
    }

    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> CreateToken(MaskinPortenClientOptions options)
        => async (HttpRequestMessage request, CancellationToken cancellationToken)
        =>
        {
            if (request.Content is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var content = await request.Content.ReadAsFormDataAsync(cancellationToken);
            content.GetValues("grant_type").ShouldHaveSingleItem().ShouldBe("urn:ietf:params:oauth:grant-type:jwt-bearer");
            var assertion = content.GetValues("assertion").ShouldHaveSingleItem();

            var parameters = new TokenValidationParameters
            {
                ValidAudiences = [FakeHttpEndpoint.HttpUri.OriginalString],
                ValidIssuers = [options.ClientId!],
                IssuerSigningKeys = [PublicKey],
                ValidateLifetime = false, // we're using fake dates here, and the handler is not configurable
            };
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(assertion, parameters, out _);
            principal.ShouldNotBeNull();

            var scope = principal.FindFirstValue("scope");
            scope.ShouldNotBeNullOrEmpty();

            var responseContent = JsonContent.Create(
                new
                {
                    access_token = $"{options.ClientId!}:{scope}:{_timeProvider.GetUtcNow()}",
                    expires_in = options.TokenDuration!.Value.TotalSeconds,
                    scope,
                    token_type = "access_token",
                },
                mediaType: null,
                options: JsonSerializerOptions.Web);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent,
            };
        };
    
    private MaskinPortenClientOptions Configure(string name, Action<MaskinPortenClientOptions> configure) 
    {
        var options = Configure(name);
        configure(options);
        return options;
    }

    private MaskinPortenClientOptions Configure(string name)
    {
        var random = Random.Shared;

        var options = _options.Get(name);
        options.Endpoint = FakeHttpEndpoint.HttpUri;
        options.Audience = FakeHttpEndpoint.HttpUri.OriginalString;
        options.TokenDuration = TimeSpan.FromMinutes(2);
        options.ClientId = Guid.NewGuid().ToString();
        options.Scope = $"fake-scope-{random.Next(0, 100)} fake-scope-{random.Next(0, 100)}";
        
        if (random.NextDouble() > 0.8)
        {
            options.Resource = $"fake-resource-{random.Next(0, 100)}";
        }

        if (random.NextDouble() > 0.8)
        {
            options.ConsumerOrg = $"fake-org-{random.Next(0, 100)}";
        }

        options.Key = PrivateKey;
        return options;
    }

    public async ValueTask DisposeAsync()
    {
        await _httpClientFactory.DisposeAsync();
    }

    private sealed class TestOptionsMonitor()
        : IOptionsMonitor<MaskinPortenClientOptions>
    {
        private ConcurrentDictionary<string, MaskinPortenClientOptions> _options = new();

        public MaskinPortenClientOptions CurrentValue => Get(Microsoft.Extensions.Options.Options.DefaultName);

        public MaskinPortenClientOptions Get(string? name)
            => _options.GetOrAdd(name ?? Microsoft.Extensions.Options.Options.DefaultName, static _ => new());

        public IDisposable? OnChange(Action<MaskinPortenClientOptions, string?> listener)
            => null;
    }
}
