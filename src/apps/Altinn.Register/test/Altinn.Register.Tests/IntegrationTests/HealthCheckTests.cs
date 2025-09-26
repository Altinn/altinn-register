using System.Net;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Register.Tests.IntegrationTests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IAccessTokenGenerator, TestAccessTokenGenerator>();
                });
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
