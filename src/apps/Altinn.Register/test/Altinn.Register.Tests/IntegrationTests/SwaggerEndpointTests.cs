using System.Text.Json;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Tests.IntegrationTests.Utils;
using Altinn.Register.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Tests.IntegrationTests;

public class SwaggerEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SwaggerEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Altinn:IsTest", "true");
            builder.UseSetting(AltinnPreStartLogger.DisableConfigKey, "true");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAccessTokenGenerator, TestAccessTokenGenerator>();
                services.AddSingleton<IExternalRoleDefinitionPersistence, MockExternalRoleDefinitionPersistence>();
            });
        });
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task SwaggerDoc_OK(string doc)
    {
        string requestUri = $"swagger/{doc}/swagger.json";

        using var client = _factory.CreateClient();

        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

        using var response = await client.SendAsync(httpRequestMessage);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseText);

        Assert.NotNull(jsonDoc);
    }
}
