using System.Net;
using System.Net.Http.Json;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.TestUtils.Http;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Operations;
using Altinn.Register.Core.Parties;
using Altinn.Register.TestUtils.TestData;
using Microsoft.Extensions.Configuration;

namespace Altinn.Register.IntegrationTests.Controllers;

public class PartiesControllerTests
    : IntegrationTestBase
{
    [Theory]
    [ApiSourceData]
    internal async Task PostPartyLookup_ValidTokenRequestForExistingOrganization_ReturnsParty(ApiSource source)
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var orgNo = org.OrganizationIdentifier.Value!.ToString();

        SetSource(source);
        if (source == ApiSource.A2)
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
    [ApiSourceData]
    internal async Task PostPartyLookup_ValidTokenRequestForExistingPerson_ReturnsParty(ApiSource source)
    {
        var person = await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct));
        var ssn = person.PersonIdentifier.Value!.ToString();

        SetSource(source);
        if (source == ApiSource.A2)
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
    [ApiSourceData]
    internal async Task PostPartyLookup_ValidTokenRequestForNonExistingOrganization_ReturnsNotFound(ApiSource source)
    {
        var orgNo = await GetRequiredService<RegisterTestDataGenerator>().GetNewOrgNumber(CancellationToken);

        SetSource(source);
        if (source == ApiSource.A2)
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
    [ApiSourceData]
    internal async Task PostPartyLookup_InvalidToken_ReturnsUnauthorized(ApiSource source)
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
    [ApiSourceData]
    internal async Task PostPartyLookup_InvalidOrgNo_ReturnsValidationProblem(ApiSource source)
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
    [ApiSourceData]
    internal async Task PostPartyLookup_InvalidSsn_ReturnsValidationProblem(ApiSource source)
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
    [ApiSourceData]
    internal async Task PostPartyLookup_MissingIdentifiers_ReturnsValidationProblem(ApiSource source)
    {
        SetSource(source);

        var response = await HttpClient.PostAsJsonAsync(
            "register/api/v1/parties/lookup",
            new Contracts.V1.PartyLookup(),
            CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        await AssertValidationError(response, ValidationErrors.MutuallyExclusive.ErrorCode);
    }

    [Theory]
    [ApiSourceData]
    internal async Task PostPartyLookup_BothIdentifiersProvided_ReturnsValidationProblem(ApiSource source)
    {
        var person = await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct));
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));

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
        await AssertValidationError(response, StdValidationErrors.Required.ErrorCode);
    }

    [Fact]
    internal async Task PostPartyLookup_EndpointSourceOverride_UsesEndpointSource()
    {
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var orgNo = org.OrganizationIdentifier.Value!.ToString();

        SetSource(ApiSource.A2);
        SetSourceForEndpoint("parties/lookup", ApiSource.DB);

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

    private void SetSource(ApiSource source)
    {
        var sourceString = source switch
        {
            ApiSource.A2 => "a2",
            ApiSource.DB => "db",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

        Configuration.AddInMemoryCollection([
            new("Altinn:register:ApiSource:Default", sourceString),
        ]);
    }

    private void SetSourceForEndpoint(string endpointName, ApiSource source)
    {
        var sourceString = source switch
        {
            ApiSource.A2 => "a2",
            ApiSource.DB => "db",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

        Configuration.AddInMemoryCollection([
            new($"Altinn:register:ApiSource:Endpoints:{endpointName}", sourceString),
        ]);
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
}
