using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Altinn.Register.Configuration;
using Altinn.Register.Contracts.V1;
using Altinn.Register.Tests.IntegrationTests.Utils;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Altinn.Register.Tests.IntegrationTests;

public class PartiesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactorySetup _webApplicationFactorySetup;
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public PartiesControllerTests(WebApplicationFactory<Program> factory)
    {
        _webApplicationFactorySetup = new WebApplicationFactorySetup(factory);

        GeneralSettings generalSettings = new() { BridgeApiEndpoint = "http://localhost/" };
        _webApplicationFactorySetup.GeneralSettingsOptions.Setup(s => s.Value).Returns(generalSettings);
    }

    [Fact]
    public async Task GetPartyList_ValidInput_OK()
    {
        // Arrange
        List<int> partyIds = [50004216, 50004219];
        List<Party> expectedParties =
        [
            await TestDataLoader.Load<Party>("50004216"),
            await TestDataLoader.Load<Party>("50004219")
        ];

        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;
            List<Party> partyList = new List<Party>();

            foreach (int id in partyIds)
            {
                partyList.Add(await TestDataLoader.Load<Party>(id.ToString()));
            }

            return new HttpResponseMessage() { Content = JsonContent.Create(partyList) };
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

        HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist") { Content = requestBody };
        testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest, CancellationToken);
        string responseContent = await response.Content.ReadAsStringAsync(CancellationToken);

        List<Party> actualParties = JsonSerializer.Deserialize<List<Party>>(
            responseContent, _options);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/parties?fetchsubunits=false", sblRequest.RequestUri.ToString().ToLower());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertionUtil.AssertEqual(expectedParties, actualParties);
    }

    [Fact]
    public async Task GetPartyList_OveridingDefaultFetchSubUnits_OK()
    {
        // Arrange
        List<int> partyIds = [50004216, 50004219];
        List<Party> expectedParties =
        [
            await TestDataLoader.Load<Party>("50004216"),
            await TestDataLoader.Load<Party>("50004219")
        ];

        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;
            List<Party> partyList = new List<Party>();

            foreach (int id in partyIds)
            {
                partyList.Add(await TestDataLoader.Load<Party>(id.ToString()));
            }

            return new HttpResponseMessage() { Content = JsonContent.Create(partyList) };
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

        HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist?fetchSubUnits=true") { Content = requestBody };
        testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest, CancellationToken);
        string responseContent = await response.Content.ReadAsStringAsync(CancellationToken);

        List<Party> actualParties = JsonSerializer.Deserialize<List<Party>>(
            responseContent, _options);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/parties?fetchsubunits=true", sblRequest.RequestUri.ToString().ToLower());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertionUtil.AssertEqual(expectedParties, actualParties);
    }

    [Fact]
    public async Task GetPartyListByUuid_ValidInput_OK()
    {
        // Arrange
        List<Party> expectedParties =
        [
            await TestDataLoader.Load<Party>("50004216"),
            await TestDataLoader.Load<Party>("50004219")
        ];
        List<Guid> partyUuids = expectedParties.Select(p => p.PartyUuid).OfType<Guid>().ToList();

        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;
            List<Party> partyList = new List<Party>();

            foreach (int id in expectedParties.Select(p => p.PartyId))
            {
                partyList.Add(await TestDataLoader.Load<Party>(id.ToString()));
            }

            return new HttpResponseMessage() { Content = JsonContent.Create(partyList) };
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

        HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid") { Content = requestBody };
        testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest, CancellationToken);
        string responseContent = await response.Content.ReadAsStringAsync(CancellationToken);

        List<Party> actualParties = JsonSerializer.Deserialize<List<Party>>(
            responseContent, _options);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/parties/byuuid?fetchsubunits=false", sblRequest.RequestUri.ToString().ToLower());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertionUtil.AssertEqual(expectedParties, actualParties);
    }

    [Fact]
    public async Task GetPartyListByUuid_OveridingDefaultFetchSubUnits_OK()
    {
        // Arrange
        List<Party> expectedParties =
        [
            await TestDataLoader.Load<Party>("50004216"),
            await TestDataLoader.Load<Party>("50004219")
        ];
        List<Guid> partyUuids = expectedParties.Select(p => p.PartyUuid).OfType<Guid>().ToList();

        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;
            List<Party> partyList = new List<Party>();

            foreach (int id in expectedParties.Select(p => p.PartyId))
            {
                partyList.Add(await TestDataLoader.Load<Party>(id.ToString()));
            }

            return new HttpResponseMessage() { Content = JsonContent.Create(partyList) };
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");

        HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylistbyuuid?fetchSubUnits=true") { Content = requestBody };
        testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest, CancellationToken);
        string responseContent = await response.Content.ReadAsStringAsync(CancellationToken);

        List<Party> actualParties = JsonSerializer.Deserialize<List<Party>>(
            responseContent, _options);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/parties/byuuid?fetchsubunits=true", sblRequest.RequestUri.ToString().ToLower());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertionUtil.AssertEqual(expectedParties, actualParties);
    }

    /// <summary>
    /// Test for GetPartyListForUser with no data for fail partyids
    /// </summary>
    [Fact]
    public async Task GetPartyListForPartyIds_InvalidInput_Notfound()
    {
        // Arrange
        List<int> partyIds = [1, 2];

        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            return await Task.FromResult(new HttpResponseMessage() { StatusCode = HttpStatusCode.NotFound });
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

        HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist") { Content = requestBody };
        testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
