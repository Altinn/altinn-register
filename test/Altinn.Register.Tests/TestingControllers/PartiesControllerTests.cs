using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Register.Enums;
using Altinn.Platform.Register.Models;
using Altinn.Register.Controllers;
using Altinn.Register.Core.Parties;
using Altinn.Register.Services.Interfaces;
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

using Xunit;

namespace Altinn.Register.Tests.TestingControllers
{
    public class PartiesControllerTests : IClassFixture<WebApplicationFactory<PartiesController>>
    {
        private readonly WebApplicationFactory<PartiesController> _factory;

        /// <summary>
        /// Initialises a new instance of the <see cref="PartiesControllerTests"/> class with the given WebApplicationFactory.
        /// </summary>
        /// <param name="factory">The WebApplicationFactory to use when creating a test server.</param>
        public PartiesControllerTests(WebApplicationFactory<PartiesController> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetParty_ValidTokenRequestForExistingParty_ReturnsParty()
        {
            string token = PrincipalUtil.GetToken(1);
            int partyId = 6565;

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/" + partyId);
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Party actual = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
        }

        [Fact]
        public async Task GetParty_ValidTokenRequestForInvalidParty_ReturnsParty()
        {
            string token = PrincipalUtil.GetToken(2);
            int partyId = 6565;

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/" + partyId);
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetParty_ValidTokenRequestForNonExistingParty_ReturnsNotFound()
        {
            string token = PrincipalUtil.GetToken(1);
            int partyId = 6565;

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/" + partyId);
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetParty_ExpiredToken_ReturnsUnAuthorized()
        {
            string token = PrincipalUtil.GetExpiredToken();
            int partyId = 6565;

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            HttpResponseMessage response = await client.GetAsync("/register/api/v1/parties/" + partyId);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task PostPartyLookup_ModelIsInvalid_ReturnsBadRequest()
        {
            string token = PrincipalUtil.GetToken(1);

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            PartyLookup lookUp = new PartyLookup();

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task PostPartyLookup_InputIsSsn_BackendServiceRespondsWithNull_ControllerRespondsWithNotFound()
        {
            string token = PrincipalUtil.GetToken(1);

            // Arrange
            string ssn = "27108775284";

            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.LookupPartyBySSNOrOrgNo(It.Is<string>(p => p == ssn), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            PartyLookup lookUp = new PartyLookup { Ssn = ssn };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyListById_SameLengthRequestAsResponse_ReturnsPartyList()
        {
            List<int> partyIds = new List<int> { 1, 2 };

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartiesById(It.IsAny<List<int>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesService.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Party> actual = await JsonSerializer.DeserializeAsync<List<Party>>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
            Assert.Equal(partyIds.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyListById_FetchSubUnitsTrue_ReturnsPartyList()
        {
            List<int> partyIds = new List<int> { 1, 2 };

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartiesById(It.IsAny<List<int>>(), It.Is<bool>(b => b == true), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesService.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist?fetchSubUnits=true") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Party> actual = await JsonSerializer.DeserializeAsync<List<Party>>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
            Assert.Equal(partyIds.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyListByUuid_SameLengthRequestAsResponse_ReturnsPartyList()
        {
            List<Guid> partyUuids = new List<Guid> { new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesService.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Party> actual = await JsonSerializer.DeserializeAsync<List<Party>>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
            Assert.Equal(partyUuids.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyListByUuid_FetchSubUnitsSetToTrue_ReturnsPartyList()
        {
            List<Guid> partyUuids = new List<Guid> { new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == true), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesService.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid?fetchsubunits=true") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();
            
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Party> actual = await JsonSerializer.DeserializeAsync<List<Party>>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
            Assert.Equal(partyUuids.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyByUuid_FetchParty_ReturnsParty()
        {
            // Arrange
            string token = PrincipalUtil.GetToken(1);

            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartyById(It.Is<Guid>(g => g == new Guid("93630d41-ca61-4b5c-b8fb-3346b561f6ff")), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Party actual = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
        }

        [Fact]
        public async Task GetPartyByUuid_FetchParty_ReturnsNotAuthorized()
        {
            // Arrange
            string token = PrincipalUtil.GetToken(1);

            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartyById(It.Is<Guid>(g => g == new Guid("93630d41-ca61-4b5c-b8fb-3346b561f6ff")), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyByUuid_FetchPartyExpiredToken_ReturnsNotAuthorized()
        {
            // Arrange
            string token = PrincipalUtil.GetExpiredToken();

            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            
            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyByUuid_FetchParty_ReturnsNothing()
        {
            // Arrange
            string token = PrincipalUtil.GetServiceOwnerOrgToken("ttd");

            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartyById(It.Is<Guid>(g => g == new Guid("93630d41-ca61-4b5c-b8fb-3346b561f6ff")), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyListByUuid_NoResponse_ReturnsEmptyList()
        {
            List<Guid> partyUuids = new List<Guid> { new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party>());

            HttpClient client = GetTestClient(partiesService.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Party> actual = await JsonSerializer.DeserializeAsync<List<Party>>(await response.Content.ReadAsStreamAsync());

            Assert.Empty(actual);
        }

        [Fact]
        public async Task PostPartyLookup_InputIsOrgNo_BackendServiceRespondsWithParty_ControllerRespondsWithOkAndParty()
        {
            // Arrange
            string orgNo = "555000103";

            Party party = new Party
            {
                OrgNumber = orgNo
            };

            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.LookupPartyBySSNOrOrgNo(It.Is<string>(p => p == orgNo), It.IsAny<CancellationToken>())).ReturnsAsync(party);

            HttpClient client = GetTestClient(partiesService.Object);

            PartyLookup lookUp = new PartyLookup { OrgNo = orgNo };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesService.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Party actual = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
        }

        [Fact]
        public async Task PostPartyLookup_ExpiredToken_ReturnsUnAuthorized()
        {
            string token = PrincipalUtil.GetExpiredToken();

            // Arrange
            string orgNo = "555000103";

            Mock<IPartyService> partiesService = new Mock<IPartyService>();

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            PartyLookup lookUp = new PartyLookup { OrgNo = orgNo };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            // Act
            HttpResponseMessage response = await client.PostAsync("/register/api/v1/parties/lookup", requestBody);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetParty_MissingAccessToken_ReturnsForbidden()
        {
            string token = PrincipalUtil.GetToken(1);
            int partyId = 6565;

            // Arrange
            Mock<IPartyService> partiesService = new Mock<IPartyService>();
            partiesService.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesService.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/" + partyId);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private HttpClient GetTestClient(IPartyService partiesService)
        {
            string projectDir = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(projectDir, "appsettings.json");

            HttpClient client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(partiesService);

                    // Set up mock authentication so that not well known endpoint is used
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                    services.AddSingleton<IAuthorization, AuthorizationWrapperMock>();
                });
                builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });
            }).CreateClient();

            return client;
        }
    }
}
