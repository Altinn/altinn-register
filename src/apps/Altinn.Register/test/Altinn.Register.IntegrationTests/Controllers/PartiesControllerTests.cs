using System.Net;
using System.Net.Http.Json;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.TestUtils.Http;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Operations;
using Altinn.Register.Core.Parties;
using Altinn.Register.TestUtils.TestData;
using Microsoft.Extensions.Configuration;

namespace Altinn.Register.IntegrationTests.Controllers;

public class PartiesControllerTests
    : IntegrationTestBase
{
    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_ValidTokenRequestForExistingOrganization_ReturnsParty(TestApiSource source)
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var orgNo = org.OrganizationIdentifier.Value!.ToString();

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            FakeHttpHandlers.For<IV1PartyService>()
                .Expect(HttpMethod.Post, "/parties/lookupObject")
                .Respond(() => JsonContent.Create(V1PartyMapper.ToV1Party(org)));
        }

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup { OrgNo = orgNo },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.Party>();

        actual.ShouldSatisfyAllConditions([
            (Contracts.V1.Party p) => p.PartyId.ShouldBe((int)org.PartyId.Value),
            (Contracts.V1.Party p) => p.PartyUuid.ShouldBe(org.PartyUuid.Value),
            (Contracts.V1.Party p) => p.PartyTypeName.ShouldBe(Contracts.V1.PartyType.Organisation),
            (Contracts.V1.Party p) => p.OrgNumber.ShouldBe(orgNo),
            (Contracts.V1.Party p) => p.Name.ShouldBe(org.DisplayName.Value),
            (Contracts.V1.Party p) => p.UnitType.ShouldBe(org.UnitType.Value),
            (Contracts.V1.Party p) => p.Organization.ShouldNotBeNull(),
            (Contracts.V1.Party p) => p.Person.ShouldBeNull(),
        ]);

        actual.Organization!.ShouldSatisfyAllConditions([
            (Contracts.V1.Organization o) => o.OrgNumber.ShouldBe(orgNo),
            (Contracts.V1.Organization o) => o.Name.ShouldBe(org.DisplayName.Value),
            (Contracts.V1.Organization o) => o.UnitType.ShouldBe(org.UnitType.Value),
        ]);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_ValidTokenRequestForExistingPerson_ReturnsParty(TestApiSource source)
    {
        var person = await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct));
        var ssn = person.PersonIdentifier.Value!.ToString();

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            FakeHttpHandlers.For<IV1PartyService>()
                .Expect(HttpMethod.Post, "/parties/lookupObject")
                .Respond(() => JsonContent.Create(V1PartyMapper.ToV1Party(person)));
        }

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup { Ssn = ssn },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.Party>();

        actual.ShouldSatisfyAllConditions([
            (Contracts.V1.Party p) => p.PartyId.ShouldBe((int)person.PartyId.Value),
            (Contracts.V1.Party p) => p.PartyUuid.ShouldBe(person.PartyUuid.Value),
            (Contracts.V1.Party p) => p.PartyTypeName.ShouldBe(Contracts.V1.PartyType.Person),
            (Contracts.V1.Party p) => p.SSN.ShouldBe(ssn),
            (Contracts.V1.Party p) => p.Name.ShouldBe(person.ShortName.Value),
            (Contracts.V1.Party p) => p.Person.ShouldNotBeNull(),
            (Contracts.V1.Party p) => p.Organization.ShouldBeNull(),
        ]);

        actual.Person!.ShouldSatisfyAllConditions([
            (Contracts.V1.Person p) => p.SSN.ShouldBe(ssn),
            (Contracts.V1.Person p) => p.Name.ShouldBe(person.ShortName.Value),
            (Contracts.V1.Person p) => p.FirstName.ShouldBe(person.FirstName.Value),
            (Contracts.V1.Person p) => p.MiddleName.ShouldBe(person.MiddleName.Value),
            (Contracts.V1.Person p) => p.LastName.ShouldBe(person.LastName.Value),
        ]);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_ValidTokenRequestForNonExistingOrganization_ReturnsNotFound(TestApiSource source)
    {
        var orgNo = await GetRequiredService<RegisterTestDataGenerator>().GetNewOrgNumber(CancellationToken);

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            FakeHttpHandlers.For<IV1PartyService>()
                .Expect(HttpMethod.Post, "/parties/lookupObject")
                .Respond(HttpStatusCode.NotFound);
        }

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup { OrgNo = orgNo.ToString() },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.NotFound);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_InvalidToken_ReturnsUnauthorized(TestApiSource source)
    {
        var orgNo = await GetRequiredService<RegisterTestDataGenerator>().GetNewOrgNumber(CancellationToken);
        SetSource(source);

        var request = new HttpRequestMessage(HttpMethod.Post, "register/api/v1/parties/lookup")
        {
            Content = JsonContent.Create(new Contracts.V1.PartyLookup { OrgNo = orgNo.ToString() }),
        };
        request.Headers.Authorization = new("Bearer", "bogus");

        var response = await HttpClient.SendAsync(request, CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_InvalidOrgNo_ReturnsValidationProblem(TestApiSource source)
    {
        var validOrgNo = (await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct))).OrganizationIdentifier.Value!.ToString();
        var invalidOrgNo = ChangeLastDigit(validOrgNo);

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup { OrgNo = invalidOrgNo },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.InvalidOrganizationNumber.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_InvalidSsn_ReturnsValidationProblem(TestApiSource source)
    {
        var validSsn = (await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct))).PersonIdentifier.Value!.ToString();
        var invalidSsn = ChangeLastDigit(validSsn);

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup { Ssn = invalidSsn },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.InvalidPersonNumber.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_MissingIdentifiers_ReturnsValidationProblem(TestApiSource source)
    {
        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup(),
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, StdValidationErrors.Required.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyLookup_BothIdentifiersProvided_ReturnsValidationProblem(TestApiSource source)
    {
        var (person, org) = await Setup(async (uow, ct) =>
        {
            var createdPerson = await uow.CreatePerson(cancellationToken: ct);
            var createdOrg = await uow.CreateOrg(cancellationToken: ct);

            return (createdPerson, createdOrg);
        });

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup
            {
                Ssn = person.PersonIdentifier.Value!.ToString(),
                OrgNo = org.OrganizationIdentifier.Value!.ToString(),
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.MutuallyExclusive.ErrorCode);
    }

    [Fact]
    public async Task PostPartyLookup_EndpointSourceOverride_UsesEndpointSource()
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var orgNo = org.OrganizationIdentifier.Value!.ToString();

        SetSource(TestApiSource.A2);
        SetSourceForEndpoint("parties/lookup", TestApiSource.DB);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup { OrgNo = orgNo },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.Party>();
        actual.ShouldSatisfyAllConditions([
            (Contracts.V1.Party p) => p.PartyTypeName.ShouldBe(Contracts.V1.PartyType.Organisation),
            (Contracts.V1.Party p) => p.OrgNumber.ShouldBe(orgNo),
            (Contracts.V1.Party p) => p.Organization.ShouldNotBeNull(),
        ]);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_ValidTokenRequestForExistingOrganization_ReturnsPartyName(TestApiSource source)
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var orgNo = org.OrganizationIdentifier.Value!.ToString();

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            FakeHttpHandlers.For<IV1PartyService>()
                .Expect(HttpMethod.Post, "/parties/lookupObject")
                .Respond(() => JsonContent.Create(V1PartyMapper.ToV1Party(org)));
        }

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = [new() { OrgNo = orgNo }],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.PartyNamesLookupResult>();
        actual.PartyNames.ShouldBe([
            new Contracts.V1.PartyName
            {
                OrgNo = orgNo,
                Name = org.DisplayName.Value,
            }
        ]);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_QueryParameterHandling_ReturnsExpectedPersonName(
        TestApiSource source,
        [CombinatorialMemberData(nameof(PartyNamesLookupQueryParameterHandlingData))]
        PartyNamesLookupQueryParameterHandling queryParameters)
    {
        var person = await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct));
        var ssn = person.PersonIdentifier.Value!.ToString();

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            FakeHttpHandlers.For<IV1PartyService>()
                .Expect(HttpMethod.Post, "/parties/lookupObject")
                .Respond(() => JsonContent.Create(V1PartyMapper.ToV1Party(person)));
        }

        var response = await HttpClient.PostAsJsonAsync(
            $"register/api/v1/parties/nameslookup{queryParameters.QueryString}",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = [new() { Ssn = ssn }],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.PartyNamesLookupResult>();
        actual.PartyNames.ShouldBe([
            new Contracts.V1.PartyName
            {
                Ssn = ssn,
                Name = person.ShortName.Value,
                PersonName = queryParameters.IncludePersonName
                    ? new Contracts.V1.PersonNameComponents
                    {
                        FirstName = person.FirstName.Value,
                        MiddleName = person.MiddleName.Value,
                        LastName = person.LastName.Value,
                    }
                    : null,
            }
        ]);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_ValidTokenRequestForExistingOrganizations_ReturnsPartyNames(TestApiSource source)
    {
        var (org1, org2) = await Setup(async (uow, ct) =>
        {
            var createdOrg1 = await uow.CreateOrg(cancellationToken: ct);
            var createdOrg2 = await uow.CreateOrg(cancellationToken: ct);

            return (createdOrg1, createdOrg2);
        });
        var orgNumbers = new[] { org1.OrganizationIdentifier.Value!.ToString(), org2.OrganizationIdentifier.Value!.ToString() };

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            ConfigureA2PartyLookupResponses([
                V1PartyMapper.ToV1Party(org1),
                V1PartyMapper.ToV1Party(org2),
            ]);
        }

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties =
                [
                    new() { OrgNo = orgNumbers[0] },
                    new() { OrgNo = orgNumbers[1] },
                ],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.PartyNamesLookupResult>();
        var expected = new[]
        {
            new Contracts.V1.PartyName
            {
                OrgNo = orgNumbers[0],
                Name = org1.DisplayName.Value,
            },
            new Contracts.V1.PartyName
            {
                OrgNo = orgNumbers[1],
                Name = org2.DisplayName.Value,
            },
        };

        actual.PartyNames.ShouldNotBeNull();
        actual.PartyNames!
            .ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_PartialUnknownInput_ReturnsNullNameForUnknownParty(TestApiSource source)
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var validOrgNo = org.OrganizationIdentifier.Value!.ToString();
        var unknownOrgNo = (await GetRequiredService<RegisterTestDataGenerator>().GetNewOrgNumber(CancellationToken)).ToString();

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            ConfigureA2PartyLookupResponses([
                V1PartyMapper.ToV1Party(org),
            ]);
        }

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties =
                [
                    new() { OrgNo = unknownOrgNo },
                    new() { OrgNo = validOrgNo },
                ],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.PartyNamesLookupResult>();
        var expected = new[]
        {
            new Contracts.V1.PartyName
            {
                OrgNo = unknownOrgNo,
                Name = null,
            },
            new Contracts.V1.PartyName
            {
                OrgNo = validOrgNo,
                Name = org.DisplayName.Value,
            },
        };

        actual.PartyNames.ShouldNotBeNull();
        actual.PartyNames!
            .ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_UnknownAndDuplicateInputs_PreservesDuplicatesAndUnknowns(TestApiSource source)
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var validOrgNo = org.OrganizationIdentifier.Value!.ToString();
        var unknownOrgNo = (await GetRequiredService<RegisterTestDataGenerator>().GetNewOrgNumber(CancellationToken)).ToString();

        SetSource(source);
        if (source == TestApiSource.A2)
        {
            ConfigureA2PartyLookupResponses([
                V1PartyMapper.ToV1Party(org),
            ]);
        }

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties =
                [
                    new() { OrgNo = unknownOrgNo },
                    new() { OrgNo = validOrgNo },
                    new() { OrgNo = validOrgNo },
                ],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.PartyNamesLookupResult>();
        var expected = new[]
        {
            new Contracts.V1.PartyName
            {
                OrgNo = unknownOrgNo,
                Name = null,
            },
            new Contracts.V1.PartyName
            {
                OrgNo = validOrgNo,
                Name = org.DisplayName.Value,
            },
            new Contracts.V1.PartyName
            {
                OrgNo = validOrgNo,
                Name = org.DisplayName.Value,
            },
        };

        actual.PartyNames.ShouldNotBeNull();
        actual.PartyNames!
            .ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public async Task PostPartyNamesLookup_EndpointSourceOverride_UsesEndpointSource()
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var orgNo = org.OrganizationIdentifier.Value!.ToString();

        SetSource(TestApiSource.A2);
        SetSourceForEndpoint("parties/nameslookup", TestApiSource.DB);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = [new() { OrgNo = orgNo }],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.PartyNamesLookupResult>();
        actual.PartyNames.ShouldBe([
            new Contracts.V1.PartyName
            {
                OrgNo = orgNo,
                Name = org.DisplayName.Value,
            }
        ]);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_InvalidQueryParameter_ReturnsBadRequest(
        TestApiSource source,
        [CombinatorialMemberData(nameof(PartyNamesLookupInvalidQueryParameters))] string queryString)
    {
        var person = await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct));
        var ssn = person.PersonIdentifier.Value!.ToString();

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            $"register/api/v1/parties/nameslookup{queryString}",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = [new Contracts.V1.PartyLookup { Ssn = ssn }],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_MissingIdentifiers_ReturnsValidationProblem(TestApiSource source)
    {
        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = [new Contracts.V1.PartyLookup()],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, StdValidationErrors.Required.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_BothIdentifiersProvided_ReturnsValidationProblem(TestApiSource source)
    {
        var (person, org) = await Setup(async (uow, ct) =>
        {
            var createdPerson = await uow.CreatePerson(cancellationToken: ct);
            var createdOrg = await uow.CreateOrg(cancellationToken: ct);

            return (createdPerson, createdOrg);
        });

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties =
                [
                    new Contracts.V1.PartyLookup
                    {
                        Ssn = person.PersonIdentifier.Value!.ToString(),
                        OrgNo = org.OrganizationIdentifier.Value!.ToString(),
                    }
                ],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.MutuallyExclusive.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_InvalidOrgNo_ReturnsValidationProblem(TestApiSource source)
    {
        var validOrgNo = (await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct))).OrganizationIdentifier.Value!.ToString();
        var invalidOrgNo = ChangeLastDigit(validOrgNo);

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = [new Contracts.V1.PartyLookup { OrgNo = invalidOrgNo }],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.InvalidOrganizationNumber.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_InvalidSsn_ReturnsValidationProblem(TestApiSource source)
    {
        var validSsn = (await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct))).PersonIdentifier.Value!.ToString();
        var invalidSsn = ChangeLastDigit(validSsn);

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = [new Contracts.V1.PartyLookup { Ssn = invalidSsn }],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.InvalidPersonNumber.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_InvalidSecondItem_ReturnsValidationProblem(TestApiSource source)
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var validOrgNo = org.OrganizationIdentifier.Value!.ToString();
        var invalidOrgNo = ChangeLastDigit(validOrgNo);

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties =
                [
                    new Contracts.V1.PartyLookup { OrgNo = validOrgNo },
                    new Contracts.V1.PartyLookup { OrgNo = invalidOrgNo },
                ],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.InvalidOrganizationNumber.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_InvalidFirstItem_ReturnsValidationProblem(TestApiSource source)
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var validOrgNo = org.OrganizationIdentifier.Value!.ToString();
        var invalidOrgNo = ChangeLastDigit(validOrgNo);

        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties =
                [
                    new Contracts.V1.PartyLookup { OrgNo = invalidOrgNo },
                    new Contracts.V1.PartyLookup { OrgNo = validOrgNo },
                ],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.InvalidOrganizationNumber.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_MultipleInvalidItems_ReturnsAllValidationErrors(TestApiSource source)
    {
        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties =
                [
                    new Contracts.V1.PartyLookup(),
                    new Contracts.V1.PartyLookup
                    {
                        Ssn = "01039012345",
                        OrgNo = "123456789",
                    },
                ],
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        var actual = await response.ShouldHaveJsonContent<AltinnValidationProblemDetails>();
        actual.ErrorCode.ShouldBe(StdProblemDescriptors.ErrorCodes.ValidationError);
        actual.Errors.ShouldContain(e => e.ErrorCode == StdValidationErrors.Required.ErrorCode);
        actual.Errors.ShouldContain(e => e.ErrorCode == ValidationErrors.MutuallyExclusive.ErrorCode);
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_NullItem_ReturnsValidationProblem(TestApiSource source)
    {
        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new
            {
                parties = new object?[]
                {
                    null,
                },
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        var actual = await response.ShouldHaveJsonContent<AltinnValidationProblemDetails>();
        actual.ErrorCode.ShouldBe(StdProblemDescriptors.ErrorCodes.ValidationError);
        actual.Errors.ShouldContain(e => e.ErrorCode == StdValidationErrors.Required.ErrorCode);
        actual.Errors.ShouldContain(e => e.Paths.Contains("/parties/0"));
    }

    [Theory]
    [CombinatorialData]
    public async Task PostPartyNamesLookup_TooManyItems_ReturnsValidationProblem(TestApiSource source)
    {
        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/nameslookup",
            new Contracts.V1.PartyNamesLookup
            {
                Parties = Enumerable.Repeat(new Contracts.V1.PartyLookup(), 1001).ToArray(),
            },
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        var actual = await response.ShouldHaveJsonContent<AltinnValidationProblemDetails>();
        actual.ErrorCode.ShouldBe(StdProblemDescriptors.ErrorCodes.ValidationError);
        actual.Errors.ShouldContain(e => e.ErrorCode == ValidationErrors.TooManyItems.ErrorCode);
        actual.Errors.ShouldContain(e => e.Paths.Contains("/parties"));
        actual.Errors.ShouldNotContain(e => e.ErrorCode == StdValidationErrors.Required.ErrorCode);
    }

    private void SetSource(TestApiSource source)
    {
        SetSourceString(source switch
        {
            TestApiSource.A2 => "a2",
            TestApiSource.DB => "db",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        });
    }

    private void SetSourceForEndpoint(string endpointName, TestApiSource source)
    {
        var sourceString = source switch
        {
            TestApiSource.A2 => "a2",
            TestApiSource.DB => "db",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

        Configuration.AddInMemoryCollection([
            new($"Altinn:register:ApiSource:Endpoints:{endpointName}", sourceString),
        ]);
    }

    private void SetSourceString(string sourceString)
    {
        Configuration.AddInMemoryCollection([
            new("Altinn:register:ApiSource:Default", sourceString),
        ]);
    }

    public static IEnumerable<PartyNamesLookupQueryParameterHandling> PartyNamesLookupQueryParameterHandlingData()
    {
        yield return new(string.Empty, false);
        yield return new("?partyComponentOption=", false);
        yield return new("?partyComponentOption=person-name", true);
    }

    public static IEnumerable<string> PartyNamesLookupInvalidQueryParameters()
    {
        yield return "?partyComponentOption=none";
        yield return "?partyComponentOption=non-existent";
    }

    private void ConfigureA2PartyLookupResponses(IEnumerable<Contracts.V1.Party> parties)
    {
        var partiesByLookupValue = parties
            .SelectMany(static party =>
            {
                var values = new List<KeyValuePair<string, Contracts.V1.Party>>(2);
                if (!string.IsNullOrEmpty(party.OrgNumber))
                {
                    values.Add(new(party.OrgNumber, party));
                }

                if (!string.IsNullOrEmpty(party.SSN))
                {
                    values.Add(new(party.SSN, party));
                }

                return values;
            })
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

        FakeHttpHandlers.For<IV1PartyService>().Fallback.Respond(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                var lookupValue = await request.Content!.ReadFromJsonAsync<string>(cancellationToken: cancellationToken);
                if (lookupValue is not null && partiesByLookupValue.TryGetValue(lookupValue, out var party))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(party),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
    }

    private static string ChangeLastDigit(string value)
    {
        value.ShouldNotBeNullOrWhiteSpace();

        var last = value[^1];
        var replacement = last == '0' ? '1' : '0';
        return $"{value[..^1]}{replacement}";
    }

    private static async Task AssertValidationError(HttpResponseMessage response, ErrorCode expectedErrorCode)
    {
        var actual = await response.ShouldHaveJsonContent<AltinnValidationProblemDetails>();
        actual.ErrorCode.ShouldBe(StdProblemDescriptors.ErrorCodes.ValidationError);
        actual.Errors.ShouldContain(e => e.ErrorCode == expectedErrorCode);
    }

    public readonly record struct PartyNamesLookupQueryParameterHandling(
        string QueryString,
        bool IncludePersonName);
}
