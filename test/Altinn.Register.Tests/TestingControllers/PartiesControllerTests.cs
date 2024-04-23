using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Register.Enums;
using Altinn.Platform.Register.Models;
using Altinn.Register.Controllers;
using Altinn.Register.Core.Parties;
using Altinn.Register.Models;
using Altinn.Register.Services.Interfaces;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Mocks.Authentication;
using Altinn.Register.Tests.Utils;

using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Http;
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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/" + partyId);
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesClient.Object);
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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/" + partyId);
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetParty_ExpiredToken_ReturnsUnAuthorized()
        {
            string token = PrincipalUtil.GetExpiredToken();
            int partyId = 6565;

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
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

            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.LookupPartyBySSNOrOrgNo(It.Is<string>(p => p == ssn), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            PartyLookup lookUp = new PartyLookup { Ssn = ssn };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyListById_SameLengthRequestAsResponse_ReturnsPartyList()
        {
            List<int> partyIds = new List<int> { 1, 2 };

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<int>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesClient.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<int>>(), It.Is<bool>(b => b == true), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesClient.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist?fetchSubUnits=true") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesClient.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == true), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party> { new(), new() });

            HttpClient client = GetTestClient(partiesClient.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid?fetchsubunits=true") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();
            
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

            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartyById(It.Is<Guid>(g => g == new Guid("93630d41-ca61-4b5c-b8fb-3346b561f6ff")), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Party actual = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync());

            Assert.NotNull(actual);
        }

        [Fact]
        public async Task GetPartyByUuid_FetchParty_ReturnsNotAuthorized()
        {
            // Arrange
            string token = PrincipalUtil.GetToken(1);

            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartyById(It.Is<Guid>(g => g == new Guid("93630d41-ca61-4b5c-b8fb-3346b561f6ff")), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyByUuid_FetchPartyExpiredToken_ReturnsNotAuthorized()
        {
            // Arrange
            string token = PrincipalUtil.GetExpiredToken();

            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            
            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyByUuid_FetchParty_ReturnsNothing()
        {
            // Arrange
            string token = PrincipalUtil.GetServiceOwnerOrgToken("ttd");

            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartyById(It.Is<Guid>(g => g == new Guid("93630d41-ca61-4b5c-b8fb-3346b561f6ff")), It.IsAny<CancellationToken>())).ReturnsAsync((Party)null);

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/byuuid/93630d41-ca61-4b5c-b8fb-3346b561f6ff");
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyListByUuid_NoResponse_ReturnsEmptyList()
        {
            List<Guid> partyUuids = new List<Guid> { new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartiesById(It.IsAny<List<Guid>>(), It.Is<bool>(b => b == false), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party>());

            HttpClient client = GetTestClient(partiesClient.Object);

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

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

            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.LookupPartyBySSNOrOrgNo(It.Is<string>(p => p == orgNo), It.IsAny<CancellationToken>())).ReturnsAsync(party);

            HttpClient client = GetTestClient(partiesClient.Object);

            PartyLookup lookUp = new PartyLookup { OrgNo = orgNo };

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(lookUp), Encoding.UTF8, "application/json");

            HttpRequestMessage httpRequestMessage =
                new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/lookup") { Content = requestBody };
            httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            partiesClient.VerifyAll();

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

            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
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
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartyById(It.Is<int>(o => o == partyId), It.IsAny<CancellationToken>())).ReturnsAsync(new Party());

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/" + partyId);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyIdentifiers_ExpiredToken_ReturnsUnAuthorized()
        {
            string token = PrincipalUtil.GetExpiredToken();

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/identifiers");

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyIdentifiers_MissingAccessToken_ReturnsForbidden()
        {
            string token = PrincipalUtil.GetToken(1);

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/identifiers");

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyIdentifiers_MissingFilters_ReturnsBadRequest()
        {
            string token = PrincipalUtil.GetOrgToken("ttd", scope: "altinn:register/partylookup.admin");

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/identifiers");

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyIdentifiers_TooManyItemsSingleFilter_ReturnsBadRequest()
        {
            string token = PrincipalUtil.GetOrgToken("ttd", scope: "altinn:register/partylookup.admin");
            List<Guid> guids = [];

            for (int i = 0; i < 105; i++)
            {
                guids.Add(Guid.NewGuid());
            }

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            QueryString queryString = default;
            queryString = queryString.Add("uuids", string.Join(',', guids));
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/identifiers" + queryString);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyIdentifiers_TooManyItemsMultiFilter_ReturnsBadRequest()
        {
            string token = PrincipalUtil.GetOrgToken("ttd", scope: "altinn:register/partylookup.admin");
            List<int> ids = [];
            List<Guid> guids = [];

            for (int i = 0; i < 51; i++)
            {
                ids.Add(i);
            }

            for (int i = 0; i < 51; i++)
            {
                guids.Add(Guid.NewGuid());
            }

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();

            HttpClient client = GetTestClient(partiesClient.Object);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            QueryString queryString = default;
            queryString = queryString.Add("ids", string.Join(',', ids));
            queryString = queryString.Add("uuids", string.Join(',', guids));
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/identifiers" + queryString);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetPartyIdentifiers_MixedQueryFormat()
        {
            string token = PrincipalUtil.GetOrgToken("ttd", scope: "altinn:register/partylookup.admin");
            List<PartyIdentifiers> expected = [
                new() { PartyId = 1, OrgNumber = "000000001", PartyUuid = new Guid("00000000-0000-0000-0000-000000000001"), SSN = null },
                new() { PartyId = 2, OrgNumber = "000000002", PartyUuid = new Guid("00000000-0000-0000-0000-000000000002"), SSN = null },
                new() { PartyId = 3, OrgNumber = "000000003", PartyUuid = new Guid("00000000-0000-0000-0000-000000000003"), SSN = null }
            ];

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartiesById(It.Is<IEnumerable<int>>(ids => ids.Contains(1) && ids.Contains(2) && ids.Contains(3) && ids.Count() == 3), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party>(expected.Select(i => new Party { PartyId = i.PartyId, PartyUuid = i.PartyUuid, OrgNumber = i.OrgNumber })));

            HttpClient httpClient = GetTestClient(partiesClient.Object);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            QueryString queryString = default;
            queryString = queryString.Add("ids", "1,2");
            queryString = queryString.Add("ids", "3");
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/identifiers" + queryString);

            // Act
            HttpResponseMessage response = await httpClient.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var actual = await response.Content.ReadFromJsonAsAsyncEnumerable<PartyIdentifiers>().ToListAsync();

            Assert.Equal(expected.Count, actual.Count);
            Assert.Contains(expected[0], actual);
            Assert.Contains(expected[1], actual);
            Assert.Contains(expected[2], actual);
        }

        [Fact]
        public async Task GetPartyIdentifiers_MixedIdentifierTypes()
        {
            string token = PrincipalUtil.GetOrgToken("ttd", scope: "altinn:register/partylookup.admin");
            List<PartyIdentifiers> expected = [
                new() { PartyId = 1, OrgNumber = "000000001", PartyUuid = new Guid("00000000-0000-0000-0000-000000000001"), SSN = null },
                new() { PartyId = 2, OrgNumber = "000000002", PartyUuid = new Guid("00000000-0000-0000-0000-000000000002"), SSN = null },
                new() { PartyId = 3, OrgNumber = "000000003", PartyUuid = new Guid("00000000-0000-0000-0000-000000000003"), SSN = null }
            ];
            PartyIdentifiers byId = expected[0];
            PartyIdentifiers byUuid = expected[1];
            PartyIdentifiers byOrgNo = expected[2];

            // Arrange
            Mock<IPartyClient> partiesClient = new Mock<IPartyClient>();
            partiesClient.Setup(s => s.GetPartiesById(It.Is<IEnumerable<int>>(ids => ids.Contains(byId.PartyId) && ids.Count() == 1), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party>(new[] { new Party { PartyId = byId.PartyId, PartyUuid = byId.PartyUuid, OrgNumber = byId.OrgNumber } }));
            partiesClient.Setup(s => s.GetPartiesById(It.Is<IEnumerable<Guid>>(ids => ids.Contains(byUuid.PartyUuid) && ids.Count() == 1), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party>(new[] { new Party { PartyId = byUuid.PartyId, PartyUuid = byUuid.PartyUuid, OrgNumber = byUuid.OrgNumber } }));
            partiesClient.Setup(s => s.LookupPartiesBySSNOrOrgNos(It.Is<IEnumerable<string>>(ids => ids.Contains(byOrgNo.OrgNumber) && ids.Count() == 1), It.IsAny<CancellationToken>()))
                .Returns(new AsyncList<Party>(new[] { new Party { PartyId = byOrgNo.PartyId, PartyUuid = byOrgNo.PartyUuid, OrgNumber = byOrgNo.OrgNumber } }));

            HttpClient httpClient = GetTestClient(partiesClient.Object);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            QueryString queryString = default;
            queryString = queryString.Add("ids", byId.PartyId.ToString());
            queryString = queryString.Add("uuids", byUuid.PartyUuid.ToString());
            queryString = queryString.Add("orgs", byOrgNo.OrgNumber);
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/register/api/v1/parties/identifiers" + queryString);

            // Act
            HttpResponseMessage response = await httpClient.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var actual = await response.Content.ReadFromJsonAsAsyncEnumerable<PartyIdentifiers>().ToListAsync();

            Assert.Equal(expected.Count, actual.Count);
            Assert.Contains(expected[0], actual);
            Assert.Contains(expected[1], actual);
            Assert.Contains(expected[2], actual);
        }

        private HttpClient GetTestClient(IPartyClient partiesClient)
        {
            string projectDir = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(projectDir, "appsettings.json");

            HttpClient client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(partiesClient);

                    // Set up mock authentication so that not well known endpoint is used
                    services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                    services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                    services.AddSingleton<IAuthorizationClient, AuthorizationClientMock>();
                });
                builder.ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });
            }).CreateClient();

            return client;
        }
    }
}
