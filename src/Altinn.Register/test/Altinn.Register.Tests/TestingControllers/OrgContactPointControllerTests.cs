using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Register.Controllers;
using Altinn.Register.Models;
using Altinn.Register.Services.Interfaces;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Moq;
using Xunit;

namespace Altinn.Register.Tests.TestingControllers;

public class OrgContactPointControllerTests : IClassFixture<WebApplicationFactory<OrgContactPointController>>
{
    private readonly WebApplicationFactory<OrgContactPointController> _factory;

    /// <summary>
    /// Initialises a new instance of the <see cref="OrgContactPointControllerTests"/> class with the given WebApplicationFactory.
    /// </summary>
    /// <param name="factory">The WebApplicationFactory to use when creating a test server.</param>
    public OrgContactPointControllerTests(WebApplicationFactory<OrgContactPointController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrgContactPoint_ValidOrgContactPointLookup_ReturnsOrgContactPointList()
    {
        string orgNo = "836281763";
        string emailAddr = "noreply@digdir.no";
        string mobileNo = "999999999";

        OrgContactPointLookup orgContactPointLookup = new()
        {
            OrganizationNumbers = [orgNo]
        };

        OrgContactPointsList orgContactsPointList = new();
        OrgContactPoints orgContactPoints = new()
        {
            OrganizationNumber = orgNo,
            EmailList = [emailAddr],
            MobileNumberList = [mobileNo]
        };

        orgContactsPointList.ContactPointsList.Add(orgContactPoints);

        // Arrange
        Mock<IOrgContactPoint> orgContactPointService = new();
        orgContactPointService.Setup(s => s.GetContactPoints(It.Is<OrgContactPointLookup>(o => o.OrganizationNumbers.Contains(orgNo)), It.IsAny<CancellationToken>())).ReturnsAsync(orgContactsPointList);
        
        HttpClient client = GetTestClient(orgContactPointService.Object);

        HttpRequestMessage httpRequestMessage = 
            new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/organizations/contactpoint/lookup")
        {
            Content = new StringContent(JsonSerializer.Serialize(orgContactPointLookup), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        OrgContactPointsList orgContactsPointListResponse = await JsonSerializer.DeserializeAsync<OrgContactPointsList>(await response.Content.ReadAsStreamAsync());

        // Assert
        orgContactPointService.VerifyAll();
        Assert.Equal(emailAddr, orgContactsPointListResponse.ContactPointsList[0].EmailList[0]);
        Assert.Equal(mobileNo, orgContactsPointListResponse.ContactPointsList[0].MobileNumberList[0]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient GetTestClient(IOrgContactPoint orgContactPointService)
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(orgContactPointService);
            });
        }).CreateClient();

        return client;
    }
}
