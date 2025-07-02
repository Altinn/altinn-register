using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Altinn.Platform.Models.Register.V1;
using Altinn.Register.Configuration;
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
        HttpResponseMessage response = await client.SendAsync(testRequest);
        string responseContent = await response.Content.ReadAsStringAsync();

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
        HttpResponseMessage response = await client.SendAsync(testRequest);
        string responseContent = await response.Content.ReadAsStringAsync();

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
        HttpResponseMessage response = await client.SendAsync(testRequest);
        string responseContent = await response.Content.ReadAsStringAsync();

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
        HttpResponseMessage response = await client.SendAsync(testRequest);
        string responseContent = await response.Content.ReadAsStringAsync();

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
        HttpResponseMessage response = await client.SendAsync(testRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Test for PostPartyNamesLookup with all valid orgnos
    /// </summary>
    [Fact]
    public async Task PostPartyNamesLookup_ValidInput_OK()
    {
        List<int> partyIds = [50004216, 50004219];
        Dictionary<string, int> partyIdsByOrgNo = new();

        // Arrange
        List<PartyLookup> inputParties = [];
        List<PartyName> expectedResultPartyNames = [];

        foreach (int partyId in partyIds)
        {
            Party party = await TestDataLoader.Load<Party>(partyId.ToString());
            inputParties.Add(new PartyLookup { OrgNo = party.OrgNumber });
            expectedResultPartyNames.Add(new PartyName { OrgNo = party.OrgNumber, Name = party.Name });
            partyIdsByOrgNo.Add(party.OrgNumber, partyId);
        }

        PartyNamesLookup input = new PartyNamesLookup
        {
            Parties = inputParties,
        };

        PartyNamesLookupResult expectedResult = new PartyNamesLookupResult
        {
            PartyNames = expectedResultPartyNames,
        };

        HttpRequestMessage sblRequest = null;
        int sblEndpointInvoked = 0;
        DelegatingHandlerStub messageHandler = new(async (request, cancellationToken) =>
        {
            sblRequest = request;
            sblEndpointInvoked++;
            string orgNo = JsonSerializer.Deserialize<string>(await request.Content!.ReadAsStringAsync(cancellationToken));
            Party party = await TestDataLoader.Load<Party>(partyIdsByOrgNo[orgNo].ToString());

            return new HttpResponseMessage() { Content = JsonContent.Create(party) };
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        StringContent requestBody = new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json");

        HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/nameslookup") { Content = requestBody };

        testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest);
        string responseContent = await response.Content.ReadAsStringAsync();

        PartyNamesLookupResult actualResult = JsonSerializer.Deserialize<PartyNamesLookupResult>(
            responseContent, _options);

        PartyNamesLookupResult actualResultFromCache = JsonSerializer.Deserialize<PartyNamesLookupResult>(
            responseContent, _options);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/lookupObject", sblRequest.RequestUri.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        actualResult.Should().BeEquivalentTo(expectedResult);
        actualResultFromCache.Should().BeEquivalentTo(expectedResult);
        Assert.Equal(partyIds.Count, sblEndpointInvoked);
    }

    /// <summary>
    /// Test for PostPartyNamesLookup with partially valid orgnos
    /// </summary>
    [Fact]
    public async Task PostPartyNamesLookup_PartialInvalidInput_OK()
    {
        // Arrange
        Party validParty = await TestDataLoader.Load<Party>("50004219");
        PartyNamesLookup input = new PartyNamesLookup
        {
            Parties = new List<PartyLookup>()
            {
                new() { OrgNo = "123456789" },
                new() { OrgNo = validParty.OrgNumber }
            }
        };

        PartyNamesLookupResult expectedResult = new PartyNamesLookupResult
        {
            PartyNames = new List<PartyName>()
            {
                new() { OrgNo = "123456789", Name = null },
                new() { OrgNo = validParty.OrgNumber, Name = validParty.Name },
            }
        };

        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, cancellationToken) =>
        {
            sblRequest = request;
            string orgNo = JsonSerializer.Deserialize<string>(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (orgNo == validParty.OrgNumber)
            {
                return new HttpResponseMessage() { Content = JsonContent.Create(validParty) };
            }

            return await Task.FromResult(new HttpResponseMessage() { StatusCode = HttpStatusCode.NotFound });
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);

        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        StringContent requestBody = new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json");

        HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/nameslookup") { Content = requestBody };

        testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(testRequest);
        string responseContent = await response.Content.ReadAsStringAsync();

        PartyNamesLookupResult actualResult = JsonSerializer.Deserialize<PartyNamesLookupResult>(
            responseContent, _options);

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.EndsWith($"/lookupObject", sblRequest.RequestUri.ToString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    /// <summary>
    /// Tests the PostPartyNamesLookup with invalid input and verifies that the appropriate error response is returned.
    /// </summary>
    /// <param name="socialSecurityNumbers">The Social Security Numbers.</param>
    /// <param name="nameComponentOption">Specifies whether to include or exclude name components</param>
    [Theory]
    [MemberData(nameof(GetPartyLookupInvalidTestData))]
    public async Task PostPartyNamesLookup_InvalidInput_BadRequest(string[] socialSecurityNumbers, string nameComponentOption)
    {
        // Arrange
        PartyNamesLookup queryBody = new()
        {
            Parties = socialSecurityNumbers.Select(ssn => new PartyLookup { Ssn = ssn }).ToList()
        };

        string token = PrincipalUtil.GetToken(1);
        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        StringContent requestBody = new(JsonSerializer.Serialize(queryBody), Encoding.UTF8, "application/json");

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, $"/register/api/v1/parties/nameslookup{nameComponentOption}")
        {
            Content = requestBody
        };
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests the PostPartyNamesLookup with valid input and verifies that the name components are correctly processed.
    /// </summary>
    /// <param name="socialSecurityNumbers">The Social Security Numbers.</param>
    /// <param name="nameComponentOption">Specifies whether to include or exclude name components</param>
    [Theory]
    [MemberData(nameof(GetPartyLookupValidTestData))]
    public async Task PostPartyNamesLookup_ValidInput_NameComponents_OK(string[] socialSecurityNumbers, string nameComponentOption)
    {
        // Arrange
        List<PartyName> testPartyNames = [];
        List<int> testPartyIds = [50012345, 50012347];
        Dictionary<string, int> testPartyIdsBySsn = [];

        var loadTasks = testPartyIds.Select(async testPartyId =>
        {
            Party party = await TestDataLoader.Load<Party>(testPartyId.ToString());
            testPartyIdsBySsn[party.SSN] = testPartyId;

            testPartyNames.Add(new PartyName
            {
                Ssn = party.SSN,
                Name = party.Name,
                PersonName = party.Person != null ? new PersonNameComponents
                {
                    FirstName = party.Person.FirstName,
                    MiddleName = party.Person.MiddleName,
                    LastName = party.Person.LastName
                }
                : null
            });
        });

        await Task.WhenAll(loadTasks);

        PartyNamesLookupResult expectedResult = new()
        {
            PartyNames = nameComponentOption switch
            {
                "?partyComponentOption=person-name" => testPartyNames.Where(e => socialSecurityNumbers.Contains(e.Ssn)).ToList(),

                _ => testPartyNames.Where(e => socialSecurityNumbers.Contains(e.Ssn))
                                   .Select(matchPartyName => new PartyName { Ssn = matchPartyName.Ssn, Name = matchPartyName.Name })
                                   .ToList(),
            }
        };

        PartyNamesLookup queryBody = new()
        {
            Parties = socialSecurityNumbers.Select(ssn => new PartyLookup { Ssn = ssn }).ToList()
        };

        int sblEndpointInvoked = 0;
        HttpRequestMessage sblRequest = null;
        DelegatingHandlerStub messageHandler = new(async (request, cancellationToken) =>
        {
            sblRequest = request;
            sblEndpointInvoked++;

            string ssn = JsonSerializer.Deserialize<string>(await request.Content!.ReadAsStringAsync(cancellationToken));
            Party matchParty = await TestDataLoader.Load<Party>(testPartyIdsBySsn[ssn].ToString());

            return new HttpResponseMessage { Content = JsonContent.Create(matchParty) };
        });
        _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

        string token = PrincipalUtil.GetToken(1);
        HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        StringContent requestBody = new(JsonSerializer.Serialize(queryBody), Encoding.UTF8, "application/json");

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, $"/register/api/v1/parties/nameslookup{nameComponentOption}")
        {
            Content = requestBody
        };
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string responseContent = await response.Content.ReadAsStringAsync();
        PartyNamesLookupResult actualResult;

        try
        {
            actualResult = JsonSerializer.Deserialize<PartyNamesLookupResult>(responseContent, _options);
        } 
        catch (JsonException ex)
        {
            throw new Exception(
                $"""
                Failed to deserialize response as JSON. Response:

                {responseContent}
                """,
                ex);
        }

        // Assert
        Assert.NotNull(sblRequest);
        Assert.Equal(HttpMethod.Post, sblRequest.Method);
        Assert.Equal(testPartyIds.Count, sblEndpointInvoked);
        Assert.EndsWith("/lookupObject", sblRequest.RequestUri.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    /// <summary>
    /// Provides valid test data for testing party lookup functionality using different component options.
    /// </summary>
    /// <returns>A collection of test data, each containing two social security numbers and corresponding component option.</returns>
    public static TheoryData<string[], string> GetPartyLookupValidTestData()
    {
        return new TheoryData<string[], string>
        {
            { ["01039012345","25871999336"], string.Empty },
            { ["01039012345","25871999336"], "?partyComponentOption=" },
            { ["01039012345","25871999336"], "?partyComponentOption=person-name" }
        };
    }

    /// <summary>
    /// Provides invalid test data for testing party lookup functionality.
    /// </summary>
    /// <returns>A collection of test data, each containing two social security numbers and invalid component option.</returns>
    public static TheoryData<string[], string> GetPartyLookupInvalidTestData()
    {
        return new TheoryData<string[], string>
        {
            { ["01039012345","25871999336"], "?partyComponentOption=none" },
            { ["01039012345","25871999336"], "?partyComponentOption=non-existent" },
        };
    }
}
