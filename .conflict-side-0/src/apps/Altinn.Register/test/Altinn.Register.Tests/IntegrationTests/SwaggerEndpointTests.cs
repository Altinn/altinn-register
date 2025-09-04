using System.Text.Json;
using Altinn.Authorization.ServiceDefaults;
using Microsoft.AspNetCore.Mvc.Testing;

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
        });
    }

    [Fact]
    public async Task SwaggerDoc_OK()
    {
        const string RequestUri = "swagger/v1/swagger.json";

        using var client = _factory.CreateClient();

        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, RequestUri);

        using var response = await client.SendAsync(httpRequestMessage);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseText);

        Assert.NotNull(jsonDoc);
    }
}
