using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Altinn.Register.Tests.IntegrationTests
{
    public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HealthCheckTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
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
