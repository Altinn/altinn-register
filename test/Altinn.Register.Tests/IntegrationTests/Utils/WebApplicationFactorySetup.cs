using System.Net.Http;

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
    public class WebApplicationFactorySetup<T>
        where T : class
    {
        private readonly WebApplicationFactory<T> _webApplicationFactory;

        public WebApplicationFactorySetup(WebApplicationFactory<T> webApplicationFactory)
        {
            _webApplicationFactory = webApplicationFactory;
        }

        public Mock<ILogger<PartiesClient>> PartiesWrapperLogger { get; set; } = new();

        public Mock<ILogger<PersonClient>> PersonsWrapperLogger { get; set; } = new();

        public Mock<ILogger<OrganizationClient>> OrganizationsWrapperLogger { get; set; } = new();

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
                    services.AddSingleton<IPartyService>(
                        new PartiesClient(
                            new HttpClient(SblBridgeHttpMessageHandler),
                            GeneralSettingsOptions.Object,
                            PartiesWrapperLogger.Object,
                            MemoryCache));
                    services.AddSingleton<IPersonClient>(
                        new PersonClient(
                            new HttpClient(SblBridgeHttpMessageHandler),
                            GeneralSettingsOptions.Object,
                            PersonsWrapperLogger.Object));
                    services.AddSingleton<IOrganizationClient>(
                        new OrganizationClient(
                            new HttpClient(SblBridgeHttpMessageHandler),
                            GeneralSettingsOptions.Object,
                            OrganizationsWrapperLogger.Object));
                });
            }).CreateClient();
        }
    }
}
