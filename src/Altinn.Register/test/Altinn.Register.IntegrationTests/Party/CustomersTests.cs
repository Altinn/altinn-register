using System.Net;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Models;
using Altinn.Register.TestUtils.TestData;

namespace Altinn.Register.IntegrationTests.Party;

public class CustomersTests
    : IntegrationTestBase
{
    [Theory]
    [MemberData(nameof(CustomerRoleIdentifiers))]
    public async Task Empty(string roleIdentifier)
    {
        var org = await Setup(async (uow, ct) =>
        {
            return await uow.CreateOrg(cancellationToken: ct);
        });

        var response = await HttpClient.GetAsync($"register/api/v2/internal/parties/{org.PartyUuid.Value}/customers/ccr/{roleIdentifier}", TestContext.Current.CancellationToken);
        await response.ShouldHaveStatusCode(HttpStatusCode.NoContent);

        var content = await response.ShouldHaveJsonContent<ListObject<PartyRecord>>();
        content.Items.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(CustomerRoleIdentifiers))]
    public async Task Roles(string roleIdentifier)
    {
        var (org1, org2, org3, org4) = await Setup(async (uow, ct) =>
        {
            var role2 = await uow.CreateFakeRoleDefinition(ExternalRoleSource.CentralCoordinatingRegister, "fake", ct);

            var org1 = await uow.CreateOrg(cancellationToken: ct);
            var org2 = await uow.CreateOrg(cancellationToken: ct);
            var org3 = await uow.CreateOrg(cancellationToken: ct);
            var org4 = await uow.CreateOrg(cancellationToken: ct);

            var persistence = uow.GetPartyExternalRolePersistence();

            await persistence.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: org2.PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new(roleIdentifier, org1.PartyUuid.Value),
                    new(role2.Identifier, org1.PartyUuid.Value),
                ],
                cancellationToken: ct);

            await persistence.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: org3.PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new(roleIdentifier, org1.PartyUuid.Value),
                ],
                cancellationToken: ct);

            await persistence.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: org4.PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new(role2.Identifier, org1.PartyUuid.Value),
                ],
                cancellationToken: ct);

            return (org1, org2, org3, org4);
        });

        var response = await HttpClient.GetAsync($"register/api/v2/internal/parties/{org1.PartyUuid.Value}/customers/ccr/{roleIdentifier}", TestContext.Current.CancellationToken);
        await response.ShouldHaveStatusCode(HttpStatusCode.OK);

        var content = await response.ShouldHaveJsonContent<ListObject<PartyRecord>>();
        var items = content.Items.ToList();
        items.Count.ShouldBe(2);
        items.ShouldContain(p => p.PartyUuid == org2.PartyUuid);
        items.ShouldContain(p => p.PartyUuid == org3.PartyUuid);
    }

    public static TheoryData<string> CustomerRoleIdentifiers => new TheoryData<string>([
        "revisor",
        "regnskapsforer",
    ]);
}
