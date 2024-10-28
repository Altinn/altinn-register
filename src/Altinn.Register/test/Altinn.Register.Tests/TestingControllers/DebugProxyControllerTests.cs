#nullable enable

using System.Net;
using System.Net.Http.Json;
using Altinn.Common.AccessToken.KeyProvider;
using Altinn.Register.Configuration;
using Altinn.Register.Controllers;
using Altinn.Register.Services.Interfaces;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Mocks.Authentication;
using Altinn.Register.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Azure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Tests.TestingControllers;

public class DebugProxyControllerTests
    : IClassFixture<WebApplicationFactory<Program>>
    , IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly MockMessageHandler _a2Handler;

    public DebugProxyControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;

        var settings = _factory.Services.GetRequiredService<IOptions<GeneralSettings>>();

        _a2Handler = new MockMessageHandler(settings.Value.BridgeApiEndpoint);
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                services.AddSingleton<IAuthorizationClient, AuthorizationClientMock>();
                services.AddHttpClient(nameof(DebugProxyController))
                    .ConfigurePrimaryHttpMessageHandler(services => _a2Handler.Create(services));
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization = new("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:register/debug.internal"));
    }

    [Fact]
    public async Task Proxies_PartyChange_Requests()
    {
        _a2Handler.MapGet("parties/partychanges/{changeId}", (HttpRequestMessage request) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new TestData 
                { 
                    Foo = "foo",
                    Bar = "bar",
                }),
                Headers =
                {
                    Location = new("https://test.altinn.example.com/register"),
                }
            };

            return response;
        });

        using var response = await _client.GetAsync("register/api/v0/debug/parties/partychanges/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().Be("https://test.altinn.example.com/register");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var data = await response.Content.ReadFromJsonAsync<TestData>();
        Assert.NotNull(data);
        data.Foo.Should().Be("foo");
        data.Bar.Should().Be("bar");
    }

    [Fact]
    public async Task Proxies_Forwards_Query()
    {
        _a2Handler.MapGet("parties/partychanges/{changeId}", (HttpRequestMessage request) =>
        {
            request.RequestUri!.Query.Should().Be("?query=1");
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var response = await _client.GetAsync("register/api/v0/debug/parties/partychanges/1?query=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private record class TestData
    {
        public required string Foo { get; init; }

        public required string Bar { get; init; }
    }
}
