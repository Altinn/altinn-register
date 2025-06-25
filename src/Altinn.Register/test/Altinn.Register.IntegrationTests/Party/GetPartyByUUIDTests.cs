using System.Net;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.TestUtils.TestData;

namespace Altinn.Register.IntegrationTests.Party;

public class GetPartyByUUIDTests
    : IntegrationTestBase
{
    [Fact]
    public async Task NonExistentParty()
    {
        var response = await HttpClient.GetAsync($"register/api/v2/internal/parties/{Guid.Empty}", CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.BadRequest);
        var content = await response.ShouldHaveJsonContent<AltinnProblemDetails>();

        content.ErrorCode.ShouldBe(Problems.PartyNotFound.ErrorCode);
    }

    [Fact]
    public async Task DefaultFields()
    {
        var party = await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct));

        var response = await HttpClient.GetAsync($"register/api/v2/internal/parties/{party.PartyUuid.Value}", CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var content = await response.ShouldHaveJsonContent<PartyRecord>();
        var person = content.ShouldBeOfType<PersonRecord>();

        content.PartyUuid.ShouldBe(party.PartyUuid);
        content.PartyId.ShouldBe(party.PartyId);
        content.PersonIdentifier.ShouldBe(party.PersonIdentifier);
        content.OrganizationIdentifier.ShouldBeNull();
        content.DisplayName.ShouldBe(party.DisplayName);
        content.CreatedAt.ShouldBeUnset();
        content.ModifiedAt.ShouldBeUnset();
        content.VersionId.ShouldBeUnset();
        content.User.ShouldBeUnset();

        person.FirstName.ShouldBeUnset();
        person.MiddleName.ShouldBeUnset();
        person.LastName.ShouldBeUnset();
    }

    [Fact]
    public async Task CustomFields()
    {
        var party = await Setup((uow, ct) => uow.CreatePerson(cancellationToken: ct));

        var response = await HttpClient.GetAsync($"register/api/v2/internal/parties/{party.PartyUuid.Value}?fields=party,person", CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var content = await response.ShouldHaveJsonContent<PartyRecord>();
        var person = content.ShouldBeOfType<PersonRecord>();

        content.PartyUuid.ShouldBe(party.PartyUuid);
        content.PartyId.ShouldBe(party.PartyId);
        content.PersonIdentifier.ShouldBe(party.PersonIdentifier);
        content.OrganizationIdentifier.ShouldBeNull();
        content.DisplayName.ShouldBe(party.DisplayName);
        content.CreatedAt.ShouldBe(party.CreatedAt);
        content.ModifiedAt.ShouldBe(party.ModifiedAt);
        content.VersionId.ShouldBe(party.VersionId);
        content.User.ShouldBeUnset();

        person.FirstName.ShouldBe(party.FirstName);
        person.MiddleName.ShouldBe(party.MiddleName);
        person.LastName.ShouldBe(party.LastName);
    }
}
