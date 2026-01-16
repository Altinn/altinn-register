using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests;

public class ConfigureMaskinPortenClientFromConfigurationTests
{
    [Fact]
    public void EmptyConfig_DoesNothing()
    {
        var config = CreateConfiguration([]);
        var sut = new ConfigureMaskinPortenClientFromConfiguration(config);
        var options = new MaskinPortenClientOptions();
        sut.Configure("test-client", options);

        options.ShouldSatisfyAllConditions(
            o => o.Endpoint.ShouldBeNull(),
            o => o.TokenDuration.ShouldBeNull(),
            o => o.ClientId.ShouldBeNull(),
            o => o.Scope.ShouldBeNull(),
            o => o.Resource.ShouldBeNull(),
            o => o.ConsumerOrg.ShouldBeNull(),
            o => o.Audience.ShouldBeNull(),
            o => o.Key.ShouldBeNull());
    }

    [Fact]
    public void Wrong_Name_DoesNothing()
    {
        var config = CreateConfiguration([
            new("Altinn:MaskinPorten:Clients:other-client:Endpoint", "https://other-client.example.com/"),
        ]);
        var sut = new ConfigureMaskinPortenClientFromConfiguration(config);
        var options = new MaskinPortenClientOptions();
        sut.Configure("test-client", options);

        options.Endpoint.ShouldBeNull();
    }

    [Fact]
    public void Overwrites_AllValues_FromConfiguration()
    {
        var key = JsonWebKey.Create(
            """
            {
                "kty": "EC",
                "use": "enc",
                "crv": "P-256",
                "kid": "test-key",
                "x": "qT0fEAsxlC5Z5SDsh3K-GfE29HnFzgoXVP8ZQBF0k_Q",
                "y": "D4w1HItNWU0i-5eoEe0BKaT8iXNISxqocNUKaubqZGo",
                "alg": "ECDH-ES"
            }
            """);

        var keyJson = JsonSerializer.Serialize(key, JsonSerializerOptions.Web);
        var keyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyJson));

        var config = CreateConfiguration([
            new("Altinn:MaskinPorten:Clients:test-client:Endpoint", "https://test-client.example.com/"),
            new("Altinn:MaskinPorten:Clients:test-client:TokenDuration", "00:20:00"),
            new("Altinn:MaskinPorten:Clients:test-client:ClientId", "test-client-id"),
            new("Altinn:MaskinPorten:Clients:test-client:Scope", "test-client-scope"),
            new("Altinn:MaskinPorten:Clients:test-client:Resource", "test-client-resource"),
            new("Altinn:MaskinPorten:Clients:test-client:ConsumerOrg", "test-client-consumer-org"),
            new("Altinn:MaskinPorten:Clients:test-client:Audience", "https://audience.test-client.example.com/"),
            new("Altinn:MaskinPorten:Clients:test-client:Key", keyBase64),
        ]);

        var sut = new ConfigureMaskinPortenClientFromConfiguration(config);
        var options = new MaskinPortenClientOptions() { Scope = "old-scope" };
        sut.Configure("test-client", options);

        options.ShouldSatisfyAllConditions(
            o => o.Endpoint.ShouldBe(new Uri("https://test-client.example.com/")),
            o => o.TokenDuration.ShouldBe(TimeSpan.FromMinutes(20)),
            o => o.ClientId.ShouldBe("test-client-id"),
            o => o.Scope.ShouldBe("test-client-scope"),
            o => o.Resource.ShouldBe("test-client-resource"),
            o => o.ConsumerOrg.ShouldBe("test-client-consumer-org"),
            o => o.Audience.ShouldBe("https://audience.test-client.example.com/"),
            o => o.Key.ShouldBe(key, JsonWebKeyComparer.Instance));
    }

    [Fact]
    public void Invalid_Key_Throws()
    {
        var config = CreateConfiguration([
            new("Altinn:MaskinPorten:Clients:test-client:Key", "not-base64"),
        ]);
        var sut = new ConfigureMaskinPortenClientFromConfiguration(config);
        var options = new MaskinPortenClientOptions();

        var exn = Should.Throw<FormatException>(() => sut.Configure("test-client", options));
        exn.Message.ShouldBe($"Config 'Altinn:MaskinPorten:Clients:test-client:Key' is not a valid base64-encoded JWK");
    }

    private static IConfiguration CreateConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(values);

        return builder.Build();
    }

    /// <remarks>This does not compare all properties of a json-web-key, but only the ones set in our test example.</remarks>
    private sealed class JsonWebKeyComparer
        : IEqualityComparer<JsonWebKey>
    {
        public static IEqualityComparer<JsonWebKey> Instance { get; } = new JsonWebKeyComparer();

        private JsonWebKeyComparer() 
        {
        }

        public bool Equals(JsonWebKey? x, JsonWebKey? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Kty == y.Kty
                && x.Use == y.Use
                && x.Crv == y.Crv
                && x.Kid == y.Kid
                && x.X == y.X
                && x.Y == y.Y
                && x.Alg == y.Alg;
        }

        public int GetHashCode([DisallowNull] JsonWebKey obj)
            => HashCode.Combine(
                obj.Kty,
                obj.Use,
                obj.Crv,
                obj.Kid,
                obj.X,
                obj.Y,
                obj.Alg);
    }
}

public class ConfigureMaskinPortenClientOptionsFromCommonOptionsTests
{
    private readonly IOptionsMonitor<MaskinPortenCommonOptions> _common = new TestOptionsMonitor();

    private MaskinPortenCommonOptions Common => _common.CurrentValue;

    [Fact]
    public void Populates_FromCommonConfig()
    {
        Common.Endpoint = new("https://common.example.com/");
        Common.Audience = "https://audience.common.example.com/";
        Common.TokenDuration = TimeSpan.FromMinutes(5);

        var options = new MaskinPortenClientOptions();

        var sut = new ConfigureMaskinPortenClientOptionsFromCommonOptions(_common);
        sut.Configure("test-client", options);

        options.ShouldSatisfyAllConditions(
            o => o.Endpoint.ShouldBe(new Uri("https://common.example.com/")),
            o => o.TokenDuration.ShouldBe(TimeSpan.FromMinutes(5)),
            o => o.Audience.ShouldBe("https://audience.common.example.com/"));
    }

    [Fact]
    public void DoesNot_Overwrite()
    {
        Common.Endpoint = new("https://common.example.com/");
        Common.Audience = "https://audience.common.example.com/";
        Common.TokenDuration = TimeSpan.FromMinutes(5);

        var options = new MaskinPortenClientOptions
        {
            Endpoint = new("https://client-endpoint.example.com/"),
            Audience = "https://audience.client-endpoint.example.com/",
            TokenDuration = TimeSpan.FromMinutes(20),
        };

        var sut = new ConfigureMaskinPortenClientOptionsFromCommonOptions(_common);
        sut.Configure("test-client", options);

        options.ShouldSatisfyAllConditions(
            o => o.Endpoint.ShouldBe(new Uri("https://client-endpoint.example.com/")),
            o => o.TokenDuration.ShouldBe(TimeSpan.FromMinutes(20)),
            o => o.Audience.ShouldBe("https://audience.client-endpoint.example.com/"));
    }

    private sealed class TestOptionsMonitor()
        : IOptionsMonitor<MaskinPortenCommonOptions>
    {
        private ConcurrentDictionary<string, MaskinPortenCommonOptions> _options = new();

        public MaskinPortenCommonOptions CurrentValue => Get(Microsoft.Extensions.Options.Options.DefaultName);

        public MaskinPortenCommonOptions Get(string? name)
            => _options.GetOrAdd(name ?? Microsoft.Extensions.Options.Options.DefaultName, static _ => new());

        public IDisposable? OnChange(Action<MaskinPortenCommonOptions, string?> listener)
            => null;
    }
}
