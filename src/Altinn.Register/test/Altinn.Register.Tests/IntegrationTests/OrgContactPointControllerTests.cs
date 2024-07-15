using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Register.Configuration;
using Altinn.Register.Controllers;
using Altinn.Register.Models;
using Altinn.Register.Tests.IntegrationTests.Utils;
using Altinn.Register.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;

using Xunit;

namespace Altinn.Register.Tests.IntegrationTests;

public class OrgContactPointControllerTests : IClassFixture<WebApplicationFactory<OrgContactPointController>>
{
    private readonly WebApplicationFactorySetup<OrgContactPointController> _webApplicationFactorySetup;

    public OrgContactPointControllerTests(WebApplicationFactory<OrgContactPointController> factory)
    {
        _webApplicationFactorySetup = new WebApplicationFactorySetup<OrgContactPointController>(factory);

        GeneralSettings generalSettings = new() { BridgeApiEndpoint = "http://localhost/sblbridge/register/api/" };
        _webApplicationFactorySetup.GeneralSettingsOptions.Setup(s => s.Value).Returns(generalSettings);
    }

    [Fact]
    public async Task PostLookup_CorrectInput_OutcomeSuccessful()
    {
        // Arrange
        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;
            return await Create200HttpResponseMessage(GetOrgContactPointsList());
        });

        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;
        
        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();

        OrgContactPointLookup orgContactPointLookup = new()
        {
            OrganizationNumbers = ["980123456"]
        };

        HttpRequestMessage testRequest = 
            new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/organizations/contactpoint/lookup")
            { 
                Content = new StringContent(JsonSerializer.Serialize(orgContactPointLookup), Encoding.UTF8, "application/json")
            };

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest);
        OrgContactPointsList orgContactsPointListResponse = await JsonSerializer.DeserializeAsync<OrgContactPointsList>(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.NotNull(orgContactsPointListResponse);
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostLookup_CorrectInput_OutcomeFails()
    {
        // Arrange
        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;
            return await Create500HttpResponseMessage();
        });

        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();

        OrgContactPointLookup orgContactPointLookup = new()
        {
            OrganizationNumbers = ["980123456"]
        };

        HttpRequestMessage testRequest =
            new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/organizations/contactpoint/lookup")
            {
                Content = new StringContent(JsonSerializer.Serialize(orgContactPointLookup), Encoding.UTF8, "application/json")
            };

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> Create200HttpResponseMessage(object obj)
    {
        string content = JsonSerializer.Serialize(obj);
        StringContent stringContent = new StringContent(content, Encoding.UTF8, "application/json");
        return await Task.FromResult(new HttpResponseMessage { Content = stringContent });
    }

    private static async Task<HttpResponseMessage> Create500HttpResponseMessage()
    {
        return await Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });
    }

    private static OrgContactPointsList GetOrgContactPointsList()
    {
        OrgContactPointsList orgContactsPointList = new();
        OrgContactPoints orgContactPoints = new()
        {
            OrganizationNumber = "980123456",
            EmailList = ["test@hattfjelldal.no"],
            MobileNumberList = ["666666666"]
        };

        orgContactsPointList.ContactPointsList.Add(orgContactPoints);
        return orgContactsPointList;
    }
}
