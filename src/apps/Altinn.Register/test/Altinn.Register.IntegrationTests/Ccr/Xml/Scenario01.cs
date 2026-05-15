using System.Diagnostics.CodeAnalysis;
using Altinn.Register.Contracts;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.TestUtils.TestData;

namespace Altinn.Register.IntegrationTests.Ccr.Xml;

/// <summary>
/// Missing Role to remove
/// </summary>
public class Scenario01
    : CcrXmlUpdateTestBase
{
    private OrganizationRecord _org = null!;
    private PersonRecord _personRegn = null!;
    private PersonRecord _personRevi = null!;
    private PersonRecord _personRegnOld = null!;
    private PersonRecord _personReviOld = null!;

    [StringSyntax(StringSyntaxAttribute.Xml)]
    protected override string XmlToApply
        => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <batchAjourholdXML xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="batchAjourholdXML_versjon2_1.xsd">
          <head avsender="ER" dato="20260102" kjoerenr="00091" mottaker="ALT" type="A" />
          <enhet organisasjonsnummer="{{_org.OrganizationIdentifier.Value}}" organisasjonsform="AS" hovedsakstype="E" undersakstype="NY" foersteOverfoering="N" datoFoedt="20260101" datoSistEndret="20260102">
            <samendringer data="D" felttype="REGN" endringstype="U" type="R">
              <rolleFoedselsnr>16898398653</rolleFoedselsnr>
            </samendringer>
            <samendringer data="D" felttype="REGN" endringstype="N" type="R">
              <rolleFoedselsnr>06815999639</rolleFoedselsnr>
              <fornavn>CECILIE</fornavn>
              <slektsnavn>CHRISTIANSEN</slektsnavn>
            </samendringer>
            <samendringer data="D" felttype="REVI" endringstype="U" type="R">
              <rolleFoedselsnr>08817498451</rolleFoedselsnr>
            </samendringer>
            <samendringer data="D" felttype="REVI" endringstype="N" type="R">
              <rolleFoedselsnr>31877295896</rolleFoedselsnr>
              <fornavn>DAVID</fornavn>
              <slektsnavn>DANIELSEN</slektsnavn>
            </samendringer>
          </enhet>
          <trai antallEnheter="1" avsender="ER" />
        </batchAjourholdXML>
        """;

    protected override async ValueTask Setup(IUnitOfWork uow, CancellationToken cancellationToken)
    {
        // we can specify things we want here
        _org = await uow.CreateOrg(
            unitType: "AS",
            name: "REGN REVI TEST AS",
            cancellationToken: cancellationToken);

        // gammel Regnskapsfører
        _personRegnOld = await uow.CreatePerson(
            name: PersonName.Create("Forrige", "Regnskapsfører"),
            identifier: PersonIdentifier.Parse("16898398653"),
            cancellationToken: cancellationToken);

        // ny Regnskapsfører
        _personRegn = await uow.CreatePerson(
            name: PersonName.Create("CECILIE", "CHRISTIANSEN"),
            identifier: PersonIdentifier.Parse("06815999639"),
            cancellationToken: cancellationToken);

        // ny Revisor
        _personRevi = await uow.CreatePerson(
            name: PersonName.Create("DAVID", "DANIELSEN"),
            identifier: PersonIdentifier.Parse("31877295896"),
            cancellationToken: cancellationToken);

        await uow.AddRole(ExternalRoleSource.CentralCoordinatingRegister, roleIdentifier: "regnskapsforer", from: _org.PartyUuid.Value, to: _personRegn.PartyUuid.Value, cancellationToken);
        await uow.AddRole(ExternalRoleSource.CentralCoordinatingRegister, roleIdentifier: "revisor", from: _org.PartyUuid.Value, to: _personRevi.PartyUuid.Value, cancellationToken);
    }

    protected override async ValueTask Verify(IUnitOfWork uow, CancellationToken cancellationToken)
    {
        var parties = uow.GetPartyPersistence();
        var roles = uow.GetPartyExternalRolePersistence();
        var revisor = new ExternalRoleReference(ExternalRoleSource.CentralCoordinatingRegister, "revisor");
        var regnskapsforer = new ExternalRoleReference(ExternalRoleSource.CentralCoordinatingRegister, "regnskapsforer");

        var updatedOrg = await parties.GetOrganizationByIdentifier(_org.OrganizationIdentifier.Value!, PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);

        updatedOrg.ShouldNotBeNull();
        var revisorFound = await roles.GetExternalRoleAssignmentsFromParty(partyUuid: _org.PartyUuid.Value, role: revisor, include: PartyExternalRoleAssignmentFieldIncludes.RoleAssignment, cancellationToken).ToListAsync(cancellationToken: cancellationToken);
        var regnskapsforerFound = await roles.GetExternalRoleAssignmentsFromParty(partyUuid: _org.PartyUuid.Value, role: regnskapsforer, include: PartyExternalRoleAssignmentFieldIncludes.RoleAssignment, cancellationToken).ToListAsync(cancellationToken: cancellationToken);
        regnskapsforerFound.Select(r => r.ToParty).ShouldHaveSingleItem().ShouldBe(_personRegn.PartyUuid.Value);
        revisorFound.Select(r => r.ToParty).ShouldHaveSingleItem().ShouldBe(_personRevi.PartyUuid.Value);
    }
}
