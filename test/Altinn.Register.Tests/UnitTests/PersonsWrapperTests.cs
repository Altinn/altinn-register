#nullable enable

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Configuration;
using Altinn.Register.Services.Implementation;
using Altinn.Register.Tests.Mocks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Register.Tests.UnitTests;

public class PersonsWrapperTests
{
    private Mock<IOptions<GeneralSettings>> _generalSettingsOptions = new();
    private Mock<ILogger<PersonsWrapper>> _personsWrapperLogger = new();

    public PersonsWrapperTests()
    {
        GeneralSettings generalSettings = new() { BridgeApiEndpoint = "http://localhost/" };
        _generalSettingsOptions.Setup(s => s.Value).Returns(generalSettings);
    }

    [Fact]
    public async Task GetPerson_SblBridge_finds_person_Target_returns_Person()
    {
        // Arrange
        HttpRequestMessage? sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;

            Person person = new Person { LastName = "làstnâme", FirstName = "firstname" };
            return await CreateHttpResponseMessage(person);
        });

        var target = new PersonsWrapper(
            new HttpClient(messageHandler), _generalSettingsOptions.Object, _personsWrapperLogger.Object);

        // Act
        var actual = await target.GetPerson("thisperson");

        // Assert
        Assert.NotNull(actual);
        Assert.Equal("firstname", actual.FirstName);

        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/persons", sblRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetPerson_SblBridge_response_is_NotFound_Target_returns_null()
    {
        // Arrange
        HttpRequestMessage? sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, token) =>
        {
            sblRequest = request;
            return await Task.FromResult(new HttpResponseMessage() { StatusCode = HttpStatusCode.NotFound });
        });

        var target = new PersonsWrapper(
            new HttpClient(messageHandler), _generalSettingsOptions.Object, _personsWrapperLogger.Object);

        // Act
        var actual = await target.GetPerson("thisperson");

        // Assert
        Assert.Null(actual);

        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/persons", sblRequest.RequestUri!.ToString());
    }

    private static async Task<HttpResponseMessage> CreateHttpResponseMessage(object obj)
    {
        string content = JsonSerializer.Serialize(obj);
        StringContent stringContent = new StringContent(content, Encoding.UTF8, "application/json");
        return await Task.FromResult(new HttpResponseMessage { Content = stringContent });
    }
}
