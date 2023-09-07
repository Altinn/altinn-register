using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Configuration;
using Altinn.Register.Controllers;
using Altinn.Register.Tests.IntegrationTests.Utils;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Utils;

using Microsoft.AspNetCore.Mvc.Testing;

using Xunit;

namespace Altinn.Register.Tests.IntegrationTests;

public class PartiesControllerTests : IClassFixture<WebApplicationFactory<PartiesController>>
{
    private readonly WebApplicationFactorySetup<PartiesController> _webApplicationFactorySetup;
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PartiesControllerTests(WebApplicationFactory<PartiesController> factory)
    {
        _webApplicationFactorySetup = new WebApplicationFactorySetup<PartiesController>(factory);

        GeneralSettings generalSettings = new() { BridgeApiEndpoint = "http://localhost/" };
        _webApplicationFactorySetup.GeneralSettingsOptions.Setup(s => s.Value).Returns(generalSettings);
    }

    [Fact]
    public async Task GetPartyList_ValidInput_OK()
    {
        // Arrange
        List<int> partyIds = new List<int>();
        partyIds.Add(50004216);
        partyIds.Add(50004219);
        List<Party> expectedParties = new List<Party>();
        expectedParties.Add(await TestDataLoader.Load<Party>("50004216"));
        expectedParties.Add(await TestDataLoader.Load<Party>("50004219"));

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
        HttpResponseMessage response = await client.SendAsync(testRequest);
        string responseContent = await response.Content.ReadAsStringAsync();

        List<Party> actualParties = JsonSerializer.Deserialize<List<Party>>(
            responseContent, options);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/parties", sblRequest.RequestUri.ToString());
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
        List<int> partyIds = new List<int>();
        partyIds.Add(1);
        partyIds.Add(2);

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
        HttpResponseMessage response = await client.SendAsync(testRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
