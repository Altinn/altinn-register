using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.TestUtils.Http;
using Altinn.Register.Core.A2;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Operations;
using Altinn.Register.TestUtils.TestData;
using Altinn.Register.TestUtils.Utils;
using Microsoft.Extensions.Configuration;

namespace Altinn.Register.IntegrationTests.Controllers;

public class OrganizationControllerTests
    : IntegrationTestBase
{
    [Theory]
    [ApiSourceData]
    internal async Task GetOrganization_ValidTokenRequestForExistingOrganization_ReturnsOrganization(ApiSource source)
    {
        // Setup
        var org = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));
        var orgNo = org.OrganizationIdentifier.Value!;

        SetSource(source);
        if (source == ApiSource.A2)
        {
            FakeHttpHandlers.For<IOrganizationClient>()
                .Expect(HttpMethod.Get, "/organizations/{orgNumber}")
                .WithRouteValue("orgNumber", orgNo.ToString())
                .Respond(() =>
                {
                    var converted = V1PartyMapper.ToV1Organization(org);
                    return JsonContent.Create(converted, options: JsonSerializerOptions.Web);
                });
        }

        // Run test
        var response = await HttpClient.GetAsync($"register/api/v1/organizations/{orgNo}", TestContext.Current.CancellationToken);

        // Validate response
        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var actual = await response.ShouldHaveJsonContent<Contracts.V1.Organization>();

        actual.ShouldNotBeNull();
        actual.OrgNumber.ShouldBe(orgNo.ToString());
    }

    [Theory]
    [ApiSourceData]
    internal async Task GetOrganization_ValidTokenRequestForNonExistingOrganization_ReturnsStatusNotFound(ApiSource source)
    {
        // Setup
        var orgNo = await GetRequiredService<RegisterTestDataGenerator>().GetNewOrgNumber(TestContext.Current.CancellationToken);

        SetSource(source);
        if (source == ApiSource.A2)
        {
            FakeHttpHandlers.For<IOrganizationClient>()
                .Expect(HttpMethod.Get, "/organizations/{orgNumber}")
                .WithRouteValue("orgNumber", orgNo.ToString())
                .Respond(HttpStatusCode.NotFound);
        }

        // Run test
        var response = await HttpClient.GetAsync($"register/api/v1/organizations/{orgNo}", TestContext.Current.CancellationToken);

        // Validate response
        await response.ShouldHaveStatusCode(HttpStatusCode.NotFound);
        var actual = await response.ShouldHaveJsonContent<AltinnProblemDetails>();

        actual.ShouldNotBeNull();
        actual.ErrorCode.ShouldBe(Problems.OrganizationNotFound.ErrorCode);
    }

    [Theory]
    [ApiSourceData]
    internal async Task GetOrganization_InvalidToken_ReturnsUnauthorized(ApiSource source)
    {
        // Setup
        var orgNo = await GetRequiredService<RegisterTestDataGenerator>().GetNewOrgNumber(TestContext.Current.CancellationToken);
        SetSource(source);

        // Run test
        var request = new HttpRequestMessage(HttpMethod.Get, $"register/api/v1/organizations/{orgNo}");
        request.Headers.Authorization = new("Bearer", "bogus");
        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Validate response
        await response.ShouldHaveStatusCode(HttpStatusCode.Unauthorized);
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
}

internal class ApiSourceDataAttribute
    : EnumMembersDataAttribute<ApiSource>
{
}
