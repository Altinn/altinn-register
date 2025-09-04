using System.Net;
using System.Text;
using System.Text.Json;
using Altinn.Register.Models;
using Altinn.Register.Services.Interfaces;
using Altinn.Register.Tests.TestingControllers.Utils;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Altinn.Register.Tests.TestingControllers;

public class OrgContactPointControllerTests(WebApplicationFixture fixture)
    : BaseControllerTests(fixture)
{
    private readonly Mock<IOrgContactPoint> _orgContactPointService = new();

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
        _orgContactPointService.Setup(s => s.GetContactPoints(It.Is<OrgContactPointLookup>(o => o.OrganizationNumbers.Contains(orgNo)), It.IsAny<CancellationToken>())).ReturnsAsync(orgContactsPointList);
        
        HttpClient client = CreateClient();

        HttpRequestMessage httpRequestMessage = 
            new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/organizations/contactpoint/lookup")
        {
            Content = new StringContent(JsonSerializer.Serialize(orgContactPointLookup), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        OrgContactPointsList orgContactsPointListResponse = await JsonSerializer.DeserializeAsync<OrgContactPointsList>(await response.Content.ReadAsStreamAsync());

        // Assert
        _orgContactPointService.VerifyAll();
        Assert.Equal(emailAddr, orgContactsPointListResponse.ContactPointsList[0].EmailList[0]);
        Assert.Equal(mobileNo, orgContactsPointListResponse.ContactPointsList[0].MobileNumberList[0]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(_orgContactPointService.Object);

        base.ConfigureTestServices(services);
    }
}
