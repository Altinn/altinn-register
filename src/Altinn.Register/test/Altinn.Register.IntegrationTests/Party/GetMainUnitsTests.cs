using System.Net;
using System.Net.Http.Json;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Models;
using Altinn.Register.TestUtils.TestData;

namespace Altinn.Register.IntegrationTests.Party;

public class GetMainUnitsTests
    : IntegrationTestBase
{
    [Fact]
    public async Task ByUuid()
    {
        var (subUnit, mainUnit) = await Setup(async (uow, ct) =>
        {
            var units = await uow.CreateOrgs(2, cancellationToken: ct);
            var subUnit = units[0];
            var mainUnit = units[1];

            await uow.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: subUnit.PartyUuid.Value, to: mainUnit.PartyUuid.Value, cancellationToken: ct);
            return (subUnit, mainUnit);
        });

        var requestContent = DataObject.Create(OrgUrn.PartyUuid.Create(subUnit.PartyUuid.Value));

        var response = await HttpClient.PostAsJsonAsync("register/api/v2/internal/parties/main-units", requestContent, JsonOptions, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var content = await response.ShouldHaveJsonContent<ListObject<OrganizationRecord>>();
        var items = content.Items.ToList();
        items.Count.ShouldBe(1);

        items[0].PartyUuid.ShouldBe(mainUnit.PartyUuid);
    }

    [Fact]
    public async Task ById()
    {
        var (subUnit, mainUnit) = await Setup(async (uow, ct) =>
        {
            var units = await uow.CreateOrgs(2, cancellationToken: ct);
            var subUnit = units[0];
            var mainUnit = units[1];

            await uow.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: subUnit.PartyUuid.Value, to: mainUnit.PartyUuid.Value, cancellationToken: ct);
            return (subUnit, mainUnit);
        });

        var requestContent = DataObject.Create(OrgUrn.PartyId.Create(subUnit.PartyId.Value));

        var response = await HttpClient.PostAsJsonAsync("register/api/v2/internal/parties/main-units", requestContent, JsonOptions, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var content = await response.ShouldHaveJsonContent<ListObject<OrganizationRecord>>();
        var items = content.Items.ToList();
        items.Count.ShouldBe(1);

        items[0].PartyUuid.ShouldBe(mainUnit.PartyUuid);
    }

    [Fact]
    public async Task ByOrganizationIdentifier()
    {
        var (subUnit, mainUnit) = await Setup(async (uow, ct) =>
        {
            var units = await uow.CreateOrgs(2, cancellationToken: ct);
            var subUnit = units[0];
            var mainUnit = units[1];

            await uow.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: subUnit.PartyUuid.Value, to: mainUnit.PartyUuid.Value, cancellationToken: ct);
            return (subUnit, mainUnit);
        });

        var requestContent = DataObject.Create(OrgUrn.OrganizationId.Create(subUnit.OrganizationIdentifier.Value!));

        var response = await HttpClient.PostAsJsonAsync("register/api/v2/internal/parties/main-units", requestContent, JsonOptions, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var content = await response.ShouldHaveJsonContent<ListObject<OrganizationRecord>>();
        var items = content.Items.ToList();
        items.Count.ShouldBe(1);

        items[0].PartyUuid.ShouldBe(mainUnit.PartyUuid);
    }

    [Fact]
    public async Task MultipleMainUnits()
    {
        var (subUnit, mainUnit1, mainUnit2) = await Setup(async (uow, ct) =>
        {
            var units = await uow.CreateOrgs(3, cancellationToken: ct);
            var subUnit = units[0];
            var mainUnit1 = units[1];
            var mainUnit2 = units[2];

            await uow.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: subUnit.PartyUuid.Value, to: mainUnit1.PartyUuid.Value, cancellationToken: ct);
            await uow.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "ikke-naeringsdrivende-hovedenhet", from: subUnit.PartyUuid.Value, to: mainUnit2.PartyUuid.Value, cancellationToken: ct);
            return (subUnit, mainUnit1, mainUnit2);
        });

        var requestContent = DataObject.Create(OrgUrn.PartyUuid.Create(subUnit.PartyUuid.Value));

        var response = await HttpClient.PostAsJsonAsync("register/api/v2/internal/parties/main-units", requestContent, JsonOptions, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var content = await response.ShouldHaveJsonContent<ListObject<OrganizationRecord>>();
        var items = content.Items.ToList();
        items.Count.ShouldBe(2);

        items[0].PartyUuid.ShouldBe(mainUnit1.PartyUuid);
        items[1].PartyUuid.ShouldBe(mainUnit2.PartyUuid);
    }

    [Fact]
    public async Task NoMainUnits()
    {
        var subUnit = await Setup((uow, ct) => uow.CreateOrg(cancellationToken: ct));

        var requestContent = DataObject.Create(OrgUrn.PartyUuid.Create(subUnit.PartyUuid.Value));

        var response = await HttpClient.PostAsJsonAsync("register/api/v2/internal/parties/main-units", requestContent, JsonOptions, TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCode(HttpStatusCode.OK);
        var content = await response.ShouldHaveJsonContent<ListObject<OrganizationRecord>>();

        content.Items.ShouldBeEmpty();
    }
}
