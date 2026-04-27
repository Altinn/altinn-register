using System.Diagnostics.CodeAnalysis;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.TestUtils.TestData;

namespace Altinn.Register.IntegrationTests.Ccr.Xml;

public class CcrPartialXmlUpdateTests
    : IntegrationTestBase
{
    [Fact(Skip = "Not implemented yet")]
    public async Task Template_Test()
    {
        var (org, pers) = await Setup(async (uow, ct) =>
        {
            // we can specify things we want here
            var org = await uow.CreateOrg(
                name: "Test Org",
                cancellationToken: ct);

            var dagl = await uow.CreatePerson(
                name: PersonName.Create("Dag", "L"),
                cancellationToken: ct);

            await uow.AddRole(
                ExternalRoleSource.CentralCoordinatingRegister,
                "daglig-leder",
                from: org.PartyUuid.Value,
                to: dagl.PartyUuid.Value,
                cancellationToken: ct);

            return (org, dagl);
        });

        await ApplyPartialXmlUpdate(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <I have="no" idea="how" this="should" look="like" />
            """);

        await Check(async (uow, ct) =>
        {
            var parties = uow.GetPartyPersistence();
            var roles = uow.GetPartyExternalRolePersistence();

            var updatedOrg = await parties.GetOrganizationByIdentifier(org.OrganizationIdentifier.Value!, PartyFieldIncludes.Party | PartyFieldIncludes.Organization, ct).FirstOrDefaultAsync(ct);
            var updatedRoles = await roles.GetExternalRoleAssignmentsFromParty(org.PartyUuid.Value, cancellationToken: ct).ToListAsync(ct);

            // example assertions
            updatedOrg.ShouldNotBeNull();
            updatedOrg.ShouldBe(org with
            {
                // we need to ignore the version id, since it's updated on any change
                VersionId = updatedOrg.VersionId,

                // check that the name was updated (as an example)
                DisplayName = "Updated Org Name",
            });
            updatedRoles.Count.ShouldBe(1);
        });
    }

    private async Task ApplyPartialXmlUpdate([StringSyntax(StringSyntaxAttribute.Xml)] string xml)
    {
        await Task.Yield();
        throw new NotImplementedException(xml);
    }
}
