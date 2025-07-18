using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Common.AccessToken.Services;
using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Contracts.V1;
using Altinn.Register.Controllers;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Mocks.Authentication;
using Altinn.Register.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Altinn.Register.Tests.TestingControllers
{
    public class OrganizationsControllerTests : IClassFixture<WebApplicationFactory<OrganizationsController>>
    {
        private readonly WebApplicationFactory<OrganizationsController> _factory;

        /// <summary>
        /// Initialises a new instance of the <see cref="OrganizationsControllerTests"/> class with the given WebApplicationFactory.
        /// </summary>
        /// <param name="factory">The WebApplicationFactory to use when creating a test server.</param>
        public OrganizationsControllerTests(WebApplicationFactory<OrganizationsController> factory)
        {
            _factory = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, c) =>
            {
                c.AddInMemoryCollection([
                    new(AltinnPreStartLogger.DisableConfigKey, "true"),
                ]);
            }));
        }

        [Fact]
        public async Task GetOrganization_ValidTokenRequestForExistingOrganization_ReturnsOrganization()
        {
            string token = PrincipalUtil.GetToken(1);
            string orgNo = "836281763";

            // Arrange
            Mock<IOrganizationClient> organizationsService = new Mock<IOrganizationClient>();
            organizationsService.Setup(s => s.GetOrganization(It.Is<string>(o => o == orgNo), It.IsAny<CancellationToken>())).ReturnsAsync(new Organization());

            HttpClient client = GetTestClient(organizationsService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/organizations/" + orgNo);
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            organizationsService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Organization actual = await JsonSerializer.DeserializeAsync<Organization>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
        }

        [Fact]
        public async Task GetOrganization_ValidTokenRequestForNonExistingOrganization_ReturnsStatusNotFound()
        {
            string token = PrincipalUtil.GetToken(1);
            string orgNo = "836281763";

            // Arrange
            Mock<IOrganizationClient> organizationsService = new Mock<IOrganizationClient>();
            organizationsService.Setup(s => s.GetOrganization(It.Is<string>(o => o == orgNo), It.IsAny<CancellationToken>())).ReturnsAsync((Organization)null);

            HttpClient client = GetTestClient(organizationsService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/organizations/" + orgNo);
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            organizationsService.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetOrganization_MissingPlatformAccessToken_ReturnsForbiden()
        {
            string token = PrincipalUtil.GetToken(1);
            string orgNo = "836281763";

            // Arrange
            Mock<IOrganizationClient> organizationsService = new Mock<IOrganizationClient>();
            organizationsService.Setup(s => s.GetOrganization(It.Is<string>(o => o == orgNo), It.IsAny<CancellationToken>())).ReturnsAsync(new Organization());

            HttpClient client = GetTestClient(organizationsService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/organizations/" + orgNo);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private HttpClient GetTestClient(IOrganizationClient organizationsService)
        {
            string projectDir = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(projectDir, "appsettings.json");

            HttpClient client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(organizationsService);

                    // Set up mock authentication so that not well known endpoint is used
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                });
                builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });
            }).CreateClient();

            return client;
        }
    }
}
