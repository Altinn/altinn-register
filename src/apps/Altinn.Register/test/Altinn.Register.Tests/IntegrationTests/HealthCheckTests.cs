using System.Net;
using Altinn.Authorization.ServiceDefaults;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Altinn.Register.Tests.IntegrationTests
{
    public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HealthCheckTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Altinn:IsTest", "true");
                builder.UseSetting(AltinnPreStartLogger.DisableConfigKey, "true");
            });
        }

        [Fact]
        public async Task VerifyHealthCheck_OK()
        {
            HttpClient client = GetTestClient();

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, "/health");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task VerifyAliveCheck_OK()
        {
            HttpClient client = GetTestClient();

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, "/alive");

            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private HttpClient GetTestClient()
            => _factory.CreateClient();
    }
}
