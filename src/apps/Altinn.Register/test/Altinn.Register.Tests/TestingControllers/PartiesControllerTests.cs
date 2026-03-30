using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Altinn.Common.AccessToken.Services;
using Altinn.Register.Core.Parties;
using Altinn.Register.Services.Interfaces;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Mocks.Authentication;
using Altinn.Register.Tests.TestingControllers.Utils;
using Altinn.Register.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Altinn.Register.Tests.TestingControllers
{
    public class PartiesControllerTests(WebApplicationFixture fixture)
        : BaseControllerTests(fixture)
    {
        private readonly Mock<IV1PartyService> _partiesClient = new();

        [Fact]
        public async Task PostPartyLookup_ModelIsInvalid_ReturnsBadRequest()
        {
            string token = PrincipalUtil.GetToken(1);

            // Arrange
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Contracts.V1.PartyLookup lookUp = new Contracts.V1.PartyLookup();

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task PostPartyLookup_InputIsSsn_BackendServiceRespondsWithNull_ControllerRespondsWithNotFound()
        {
            string token = PrincipalUtil.GetToken(1);

            // Arrange
            string ssn = "27108775284";

            _partiesClient.Setup(s => s.LookupPartyBySSNOrOrgNo(It.Is<string>(p => p == ssn), It.IsAny<CancellationToken>())).ReturnsAsync((Contracts.V1.Party?)null);

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Contracts.V1.PartyLookup lookUp = new Contracts.V1.PartyLookup { Ssn = ssn };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            _partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyListById_SameLengthRequestAsResponse_ReturnsPartyList()
        {
            List<int> partyIds = new List<int> { 1, 2 };

            // Arrange
            _partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<int>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Contracts.V1.Party> { new(), new() });

            HttpClient client = CreateClient();

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            _partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Contracts.V1.Party> actual = (await JsonSerializer.DeserializeAsync<List<Contracts.V1.Party>>(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken))!;

            Assert.NotNull(actual);
            Assert.Equal(partyIds.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyListById_FetchSubUnitsTrue_ReturnsPartyList()
        {
            List<int> partyIds = new List<int> { 1, 2 };

            // Arrange
            _partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<int>>(), It.Is<bool>(b => b == true), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Contracts.V1.Party> { new(), new() });

            HttpClient client = CreateClient();

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist?fetchSubUnits=true") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            _partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Contracts.V1.Party> actual = (await JsonSerializer.DeserializeAsync<List<Contracts.V1.Party>>(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken))!;

            Assert.NotNull(actual);
            Assert.Equal(partyIds.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyListByUuid_SameLengthRequestAsResponse_ReturnsPartyList()
        {
            List<Guid> partyUuids = new List<Guid> { new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            // Arrange
            _partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Contracts.V1.Party> { new(), new() });

            HttpClient client = CreateClient();

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            _partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Contracts.V1.Party> actual = (await JsonSerializer.DeserializeAsync<List<Contracts.V1.Party>>(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken))!;

            Assert.NotNull(actual);
            Assert.Equal(partyUuids.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyListByUuid_FetchSubUnitsSetToTrue_ReturnsPartyList()
        {
            List<Guid> partyUuids = new List<Guid> { new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            // Arrange
            _partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == true), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Contracts.V1.Party> { new(), new() });

            HttpClient client = CreateClient();

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid?fetchsubunits=true") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            _partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Contracts.V1.Party> actual = (await JsonSerializer.DeserializeAsync<List<Contracts.V1.Party>>(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken))!;

            Assert.NotNull(actual);
            Assert.Equal(partyUuids.Count, actual.Count);
        }

        [Fact]
        public async Task GetPartyListByUuid_NoResponse_ReturnsEmptyList()
        {
            List<Guid> partyUuids = new List<Guid> { new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            // Arrange
            _partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Contracts.V1.Party>());

            HttpClient client = CreateClient();

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            _partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<Contracts.V1.Party> actual = (await JsonSerializer.DeserializeAsync<List<Contracts.V1.Party>>(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken))!;

            Assert.Empty(actual);
        }

        [Fact]
        public async Task PostPartyLookup_InputIsOrgNo_BackendServiceRespondsWithParty_ControllerRespondsWithOkAndParty()
        {
            // Arrange
            string orgNo = "555000103";

            Contracts.V1.Party party = new Contracts.V1.Party
            {
                OrgNumber = orgNo
            };

            _partiesClient.Setup(s => s.LookupPartyBySSNOrOrgNo(It.Is<string>(p => p == orgNo), It.IsAny<CancellationToken>())).ReturnsAsync(party);

            HttpClient client = CreateClient();

            Contracts.V1.PartyLookup lookUp = new Contracts.V1.PartyLookup { OrgNo = orgNo };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage, CancellationToken);

            // Assert
            _partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Contracts.V1.Party actual = (await JsonSerializer.DeserializeAsync<Contracts.V1.Party>(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken))!;

            Assert.NotNull(actual);
        }

        [Fact]
        public async Task PostPartyLookup_ExpiredToken_ReturnsUnAuthorized()
        {
            string token = PrincipalUtil.GetExpiredToken();

            // Arrange
            string orgNo = "555000103";

            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Contracts.V1.PartyLookup lookUp = new Contracts.V1.PartyLookup { OrgNo = orgNo };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            // Act
            HttpResponseMessage response = await client.PostAsync("/register/api/v1/parties/lookup", requestBody, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.AddSingleton(_partiesClient.Object);

            // Set up mock authentication so that not well known endpoint is used
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            services.AddSingleton<IAuthorizationClient, AuthorizationClientMock>();

            base.ConfigureTestServices(services);
        }

        protected override void ConfigureTestConfiguration(IConfigurationBuilder builder)
        {
            string projectDir = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(projectDir, "appsettings.json");
            builder.AddJsonFile(configPath);

            base.ConfigureTestConfiguration(builder);
        }
    }
}
