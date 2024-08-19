using Altinn.Common.AccessToken.Services;
using Altinn.Register.Clients;
using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Configuration;
using Altinn.Register.Core.Parties;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace Altinn.Register.Tests.IntegrationTests.Utils
{
    public class WebApplicationFactorySetup
    {
        private readonly WebApplicationFactory<Program> _webApplicationFactory;

        public WebApplicationFactorySetup(WebApplicationFactory<Program> webApplicationFactory)
        {
            _webApplicationFactory = webApplicationFactory;
        }

        public Mock<ILogger<PartiesClient>> PartiesClientLogger { get; set; } = new();

        public Mock<ILogger<PersonClient>> PersonsClientLogger { get; set; } = new();

        public Mock<ILogger<OrganizationClient>> OrganizationsClientsLogger { get; set; } = new();

        public Mock<IOptions<GeneralSettings>> GeneralSettingsOptions { get; set; } = new();

        public MemoryCache MemoryCache { get; set; } = new MemoryCache(new MemoryCacheOptions());

        public HttpMessageHandler SblBridgeHttpMessageHandler { get; set; } = new DelegatingHandlerStub();

        public HttpClient GetTestServerClient()
        {
            return _webApplicationFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                    services.AddSingleton<IMemoryCache>(MemoryCache);

                    // Using the real/actual implementation of IParties and IPersons, but with a mocked message handler.
                    // Haven't found any other ways of injecting a mocked message handler to simulate SBL Bridge.
                    services.AddSingleton<IV1PartyService>(
                        new PartiesClient(
                            new HttpClient(SblBridgeHttpMessageHandler),
                            GeneralSettingsOptions.Object,
                            PartiesClientLogger.Object,
                            MemoryCache));
                    services.AddSingleton<IPersonClient>(
                        new PersonClient(
                            new HttpClient(SblBridgeHttpMessageHandler),
                            GeneralSettingsOptions.Object,
                            PersonsClientLogger.Object));
                    services.AddSingleton<IOrganizationClient>(
                        new OrganizationClient(
                            new HttpClient(SblBridgeHttpMessageHandler),
                            GeneralSettingsOptions.Object,
                            OrganizationsClientsLogger.Object));
                });
            }).CreateClient();
        }
    }
}
