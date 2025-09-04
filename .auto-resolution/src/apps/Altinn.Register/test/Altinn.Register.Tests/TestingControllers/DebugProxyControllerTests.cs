#nullable enable

using System.Net;
using System.Net.Http.Json;
using Altinn.Common.AccessToken.Services;
using Altinn.Register.Configuration;
using Altinn.Register.Controllers;
using Altinn.Register.Services.Interfaces;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Mocks.Authentication;
using Altinn.Register.Tests.TestingControllers.Utils;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils.Http;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Tests.TestingControllers;

public class DebugProxyControllerTests(WebApplicationFixture fixture)
    : BaseControllerTests(fixture)
{
    private readonly FakeHttpHandlers _httpHandlers = new();

    [Fact]
    public async Task Proxies_PartyChange_Requests()
    {
        _httpHandlers.For(nameof(DebugController))
            .Expect(HttpMethod.Get, "parties/partychanges/1")
            .Respond(() => new HttpResponseMessage(HttpStatusCode.OK)
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
            });

        var client = CreateClient();
        using var response = await client.GetAsync("register/api/v0/debug/parties/partychanges/1");

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
        _httpHandlers.For(nameof(DebugController))
            .Expect(HttpMethod.Get, "parties/partychanges/1")
            .WithQuery("query", "1")
            .Respond(() => HttpStatusCode.OK);

        var client = CreateClient();
        using var response = await client.GetAsync("register/api/v0/debug/parties/partychanges/1?query=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record class TestData
    {
        public required string Foo { get; init; }

        public required string Bar { get; init; }
    }

    protected override ValueTask Initialize(IServiceProvider services)
    {
        return base.Initialize(services);
    }

    protected override HttpClient CreateClient()
    {
        var client = base.CreateClient();

        client.DefaultRequestHeaders.Authorization = new("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:register/debug.internal"));

        return client;
    }

    protected override ValueTask DisposeAsync()
    {
        ((IDisposable)_httpHandlers).Dispose();

        return base.DisposeAsync();
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
        services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
        services.AddSingleton<IAuthorizationClient, AuthorizationClientMock>();
        services.AddFakeHttpHandlers(_httpHandlers);
        services.AddOptions<GeneralSettings>()
            .Configure(s => s.BridgeApiEndpoint = FakeHttpMessageHandler.FakeBasePath.ToString());

        base.ConfigureTestServices(services);
    }
}
