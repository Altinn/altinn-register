using System.Data;
using System.Linq.Expressions;
using System.Text;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.TestData;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace Altinn.Register.Persistence.Tests;

public class PostgreSqlPartyPersistenceTests(ITestOutputHelper output)
    : DatabaseTestBase
{
    protected override ITestOutputHelper? TestOutputHelper => output;

    private readonly static Guid OrganizationWithChildrenUuid = Guid.Parse("b6368d0a-bce4-4798-8460-f4f86fc354c2");
    private readonly static uint OrganizationWithChildrenId = 50056131;
    private readonly static OrganizationIdentifier OrganizationWithChildrenIdentifier = OrganizationIdentifier.Parse("910114166");
    private readonly static Guid ChildOrganizationUuid = Guid.Parse("08cb91ff-75a4-45a4-b141-3c6be1bf8728");
    private readonly static Guid PersonUuid = Guid.Parse("1dee49bc-7758-43d9-b5de-63c79dbe13a3");
    private readonly static PersonIdentifier PersonIdentifier = PersonIdentifier.Parse("01056261032");

    private IUnitOfWork? _unitOfWork;
    private NpgsqlConnection? _connection;
    private PostgreSqlPartyPersistence? _persistence;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        await NewTransaction();
    }

    protected override async ValueTask DisposeAsync()
    {
        if (_unitOfWork is not null)
        {
            await _unitOfWork.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private ValueTask CommitAsync()
        => _unitOfWork!.CommitAsync();

    private NpgsqlConnection Connection
        => _connection!;

    private PostgreSqlPartyPersistence Persistence
        => _persistence!;

    private IUnitOfWork UoW
        => _unitOfWork!;

    private async Task NewTransaction(bool commit = true)
    {
        if (_unitOfWork is { } uow)
        {
            if (commit)
            {
                await uow.CommitAsync();
            }

            await uow.DisposeAsync();
        }

        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        _unitOfWork = await uowManager.CreateAsync(activityName: "test");
        _connection = _unitOfWork.GetRequiredService<NpgsqlConnection>();
        _persistence = _unitOfWork.GetRequiredService<PostgreSqlPartyPersistence>();
    }

    [Fact]
    public void CanGet_IPartyPersistence()
    {
        var persistence = _unitOfWork!.GetPartyPersistence();
        persistence.Should().BeSameAs(Persistence);
    }

    [Fact]
    public void CanGet_IPartyRolePersistence()
    {
        var persistence = _unitOfWork!.GetPartyExternalRolePersistence();
        persistence.Should().BeSameAs(Persistence);
    }

    [Fact]
    public async Task GetPartyById_NoneExistingUuid_ReturnsEmpty()
    {
        var partyUuid = Guid.Parse("F0000000-0000-0000-0000-000000000000");
        var result = await Persistence.GetPartyById(partyUuid).ToListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPartyById_NoneExistingId_ReturnsEmpty()
    {
        var partyId = 0U;
        var result = await Persistence.GetPartyById(partyId).ToListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPartyById_Returns_SingleParty()
    {
        var result = await Persistence.GetPartyById(OrganizationWithChildrenUuid).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<OrganizationRecord>().Which;

        using var scope = new AssertionScope();
        party.ParentOrganizationUuid.Should().BeUnset();

        party.PartyUuid.Should().Be(OrganizationWithChildrenUuid);
        party.PartyId.Should().Be(OrganizationWithChildrenId);
        party.PartyType.Should().Be(PartyRecordType.Organization);
        party.DisplayName.Should().Be("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.Should().BeNull();
        party.OrganizationIdentifier.Should().Be(OrganizationWithChildrenIdentifier);

        party.UnitStatus.Should().BeUnset();
        party.UnitType.Should().BeUnset();
        party.TelephoneNumber.Should().BeUnset();
        party.MobileNumber.Should().BeUnset();
        party.FaxNumber.Should().BeUnset();
        party.EmailAddress.Should().BeUnset();
        party.InternetAddress.Should().BeUnset();
        party.MailingAddress.Should().BeUnset();
        party.BusinessAddress.Should().BeUnset();
    }

    [Fact]
    public async Task GetPartyById_CanGet_OrganizationData()
    {
        var result = await Persistence.GetPartyById(OrganizationWithChildrenUuid, include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<OrganizationRecord>().Which;

        using var scope = new AssertionScope();
        party.ParentOrganizationUuid.Should().BeUnset();

        party.PartyUuid.Should().Be(OrganizationWithChildrenUuid);
        party.PartyId.Should().Be(OrganizationWithChildrenId);
        party.PartyType.Should().Be(PartyRecordType.Organization);
        party.DisplayName.Should().Be("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.Should().BeNull();
        party.OrganizationIdentifier.Should().Be(OrganizationWithChildrenIdentifier);

        party.UnitStatus.Should().Be("N");
        party.UnitType.Should().Be("FLI");
        party.TelephoneNumber.Should().BeNull();
        party.MobileNumber.Should().BeNull();
        party.FaxNumber.Should().BeNull();
        party.EmailAddress.Should().Be("test@test.test");
        party.InternetAddress.Should().BeNull();
        party.MailingAddress.Should().BeNull();
        party.BusinessAddress.Should().BeNull();
    }

    [Fact]
    public async Task GetPartyByUserId_Returns_SingleParty()
    {
        var person = await UoW.CreatePerson();
        var personUserId = person.User.SelectFieldValue(static u => u.UserId).Should().HaveValue().Which;

        var result = await Persistence.GetPartyByUserId(personUserId, include: PartyFieldIncludes.Party).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<PersonRecord>().Which;

        using var scope = new AssertionScope();

        party.PartyUuid.Should().Be(person.PartyUuid);
        party.PartyId.Should().Be(person.PartyId);
        party.PartyType.Should().Be(person.PartyType);
        party.DisplayName.Should().Be(person.DisplayName);
        party.PersonIdentifier.Should().Be(person.PersonIdentifier);
        party.OrganizationIdentifier.Should().Be(person.OrganizationIdentifier);
        party.User.Should().Be(new PartyUserRecord(userId: personUserId, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(personUserId)));
    }

    [Fact]
    public async Task GetPartyByUserId_HistoricalId_Returns_SingleParty_WithMultipleUserIds()
    {
        var person = await UoW.CreatePerson();
        var personUserIds = person.User.SelectFieldValue(static u => u.UserIds).Should().HaveValue().Which;
        personUserIds.Should().HaveCountGreaterThanOrEqualTo(3);

        var personUserId = personUserIds[0];
        var historicalUserId1 = personUserIds[1];
        var historicalUserId2 = personUserIds[2];

        {
            var result = await Persistence.GetPartyByUserId(historicalUserId1, include: PartyFieldIncludes.Party).ToListAsync();

            var party = result.Should().ContainSingle().Which.Should().BeOfType<PersonRecord>().Which;

            using var scope = new AssertionScope();

            party.PartyUuid.Should().Be(person.PartyUuid);
            party.PartyId.Should().Be(person.PartyId);
            party.PartyType.Should().Be(person.PartyType);
            party.DisplayName.Should().Be(person.DisplayName);
            party.PersonIdentifier.Should().Be(person.PersonIdentifier);
            party.OrganizationIdentifier.Should().Be(person.OrganizationIdentifier);
            party.User.Should().Be(new PartyUserRecord(userId: personUserId, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(personUserId, historicalUserId1)));
        }

        {
            var result = await Persistence.GetPartyByUserId(historicalUserId2, include: PartyFieldIncludes.Party).ToListAsync();

            var party = result.Should().ContainSingle().Which.Should().BeOfType<PersonRecord>().Which;

            using var scope = new AssertionScope();

            party.PartyUuid.Should().Be(person.PartyUuid);
            party.PartyId.Should().Be(person.PartyId);
            party.PartyType.Should().Be(person.PartyType);
            party.DisplayName.Should().Be(person.DisplayName);
            party.PersonIdentifier.Should().Be(person.PersonIdentifier);
            party.OrganizationIdentifier.Should().Be(person.OrganizationIdentifier);
            party.User.Should().Be(new PartyUserRecord(userId: personUserId, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(personUserId, historicalUserId2)));
        }
    }

    [Fact]
    public async Task GetPartyByOrganizationIdentifier_CanGet_OrganizationData()
    {
        var result = await Persistence.GetOrganizationByIdentifier(OrganizationWithChildrenIdentifier, include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<OrganizationRecord>().Which;

        using var scope = new AssertionScope();
        party.ParentOrganizationUuid.Should().BeUnset();

        party.PartyUuid.Should().Be(OrganizationWithChildrenUuid);
        party.PartyId.Should().Be(OrganizationWithChildrenId);
        party.PartyType.Should().Be(PartyRecordType.Organization);
        party.DisplayName.Should().Be("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.Should().BeNull();
        party.OrganizationIdentifier.Should().Be(OrganizationWithChildrenIdentifier);

        party.UnitStatus.Should().Be("N");
        party.UnitType.Should().Be("FLI");
        party.TelephoneNumber.Should().BeNull();
        party.MobileNumber.Should().BeNull();
        party.FaxNumber.Should().BeNull();
        party.EmailAddress.Should().Be("test@test.test");
        party.InternetAddress.Should().BeNull();
        party.MailingAddress.Should().BeNull();
        party.BusinessAddress.Should().BeNull();
    }

    [Fact]
    public async Task GetPartyById_CanGet_PersonData()
    {
        var result = await Persistence.GetPartyById(PersonUuid, include: PartyFieldIncludes.Party | PartyFieldIncludes.Person).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<PersonRecord>().Which;

        using var scope = new AssertionScope();
        party.PartyUuid.Should().Be(PersonUuid);
        party.PartyId.Should().Be(50002129);
        party.PartyType.Should().Be(PartyRecordType.Person);
        party.DisplayName.Should().Be("SANNE BJØRKUM");
        party.PersonIdentifier.Should().Be(PersonIdentifier);
        party.OrganizationIdentifier.Should().BeNull();

        party.FirstName.Should().Be("SANNE");
        party.MiddleName.Should().BeNull();
        party.LastName.Should().Be("BJØRKUM");
        party.Address.Should().Be(new StreetAddressRecord { PostalCode = "3230", City = "SANDEFJORD" });
        party.MailingAddress.Should().Be(new MailingAddressRecord { Address = "Granholmveien 19", PostalCode = "3230", City = "SANDEFJORD" });
        party.DateOfBirth.Should().Be(new DateOnly(1862, 05, 01));
        party.DateOfDeath.Should().BeNull();
    }

    [Fact]
    public async Task GetPartyByPersonIdentifier_CanGet_PersonData()
    {
        var result = await Persistence.GetPersonByIdentifier(PersonIdentifier, include: PartyFieldIncludes.Party | PartyFieldIncludes.Person).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<PersonRecord>().Which;

        using var scope = new AssertionScope();
        party.PartyUuid.Should().Be(PersonUuid);
        party.PartyId.Should().Be(50002129);
        party.PartyType.Should().Be(PartyRecordType.Person);
        party.DisplayName.Should().Be("SANNE BJØRKUM");
        party.PersonIdentifier.Should().Be(PersonIdentifier);
        party.OrganizationIdentifier.Should().BeNull();

        party.FirstName.Should().Be("SANNE");
        party.MiddleName.Should().BeNull();
        party.LastName.Should().Be("BJØRKUM");
        party.Address.Should().Be(new StreetAddressRecord { PostalCode = "3230", City = "SANDEFJORD" });
        party.MailingAddress.Should().Be(new MailingAddressRecord { Address = "Granholmveien 19", PostalCode = "3230", City = "SANDEFJORD" });
        party.DateOfBirth.Should().Be(new DateOnly(1862, 05, 01));
        party.DateOfDeath.Should().BeNull();
    }

    [Fact]
    public async Task GetPartyById_CanGet_SubUnits()
    {
        var result = await Persistence.GetPartyById(
            OrganizationWithChildrenUuid,
            include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization | PartyFieldIncludes.SubUnits)
            .ToListAsync();

        result.Should().HaveCount(2);

        var parent = result[0].Should().BeOfType<OrganizationRecord>().Which;
        var child = result[1].Should().BeOfType<OrganizationRecord>().Which;

        {
            using var scope = new AssertionScope();
            parent.PartyUuid.Should().Be(OrganizationWithChildrenUuid);
            parent.PartyId.Should().Be(OrganizationWithChildrenId);
            parent.PartyType.Should().Be(PartyRecordType.Organization);
            parent.DisplayName.Should().Be("MOEN OG BJØRNEVATN");
            parent.PersonIdentifier.Should().BeNull();
            parent.OrganizationIdentifier.Should().Be(OrganizationWithChildrenIdentifier);

            parent.UnitStatus.Should().Be("N");
            parent.UnitType.Should().Be("FLI");
            parent.TelephoneNumber.Should().BeNull();
            parent.MobileNumber.Should().BeNull();
            parent.FaxNumber.Should().BeNull();
            parent.EmailAddress.Should().Be("test@test.test");
            parent.InternetAddress.Should().BeNull();
            parent.MailingAddress.Should().BeNull();
            parent.BusinessAddress.Should().BeNull();

            parent.ParentOrganizationUuid.Should().BeUnset();
        }

        {
            using var scope = new AssertionScope();
            child.PartyUuid.Should().Be(ChildOrganizationUuid);
            child.PartyId.Should().Be(50056655);
            child.PartyType.Should().Be(PartyRecordType.Organization);
            child.DisplayName.Should().Be("NERLANDSØY OG DYRANUT");
            child.PersonIdentifier.Should().BeNull();
            child.OrganizationIdentifier.Should().HaveValue().Which.Should().Be("910056077");

            child.UnitStatus.Should().Be("N");
            child.UnitType.Should().Be("BEDR");
            child.TelephoneNumber.Should().BeNull();
            child.MobileNumber.Should().BeNull();
            child.FaxNumber.Should().BeNull();
            child.EmailAddress.Should().Be("test@test.test");
            child.InternetAddress.Should().BeNull();
            child.MailingAddress.Should().BeNull();
            child.BusinessAddress.Should().BeNull();

            child.ParentOrganizationUuid.Should().Be(OrganizationWithChildrenUuid);
        }
    }

    [Fact]
    public async Task LookupParties_MultipleIdentifiers_ToSameParty_ReturnsSingleParty()
    {
        var result = await Persistence.LookupParties(
            partyUuids: [OrganizationWithChildrenUuid],
            partyIds: [OrganizationWithChildrenId],
            organizationIdentifiers: [OrganizationWithChildrenIdentifier])
            .ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<OrganizationRecord>().Which;

        using var scope = new AssertionScope();
        party.ParentOrganizationUuid.Should().BeUnset();

        party.PartyUuid.Should().Be(OrganizationWithChildrenUuid);
        party.PartyId.Should().Be(OrganizationWithChildrenId);
        party.PartyType.Should().Be(PartyRecordType.Organization);
        party.DisplayName.Should().Be("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.Should().BeNull();
        party.OrganizationIdentifier.Should().Be(OrganizationWithChildrenIdentifier);

        party.UnitStatus.Should().BeUnset();
        party.UnitType.Should().BeUnset();
        party.TelephoneNumber.Should().BeUnset();
        party.MobileNumber.Should().BeUnset();
        party.FaxNumber.Should().BeUnset();
        party.EmailAddress.Should().BeUnset();
        party.InternetAddress.Should().BeUnset();
        party.MailingAddress.Should().BeUnset();
        party.BusinessAddress.Should().BeUnset();
    }

    [Fact]
    public async Task LookupParties_CanReturn_MultipleParties()
    {
        var person1 = await UoW.CreatePerson();
        var person2 = await UoW.CreatePerson();

        var person1UserId = person1.User.SelectFieldValue(static u => u.UserId).Should().HaveValue().Which;
        var person2UserId = person2.User.SelectFieldValue(static u => u.UserId).Should().HaveValue().Which;
        var person2HistoricalUserId = person2.User.SelectFieldValue(static u => u.UserIds).Should().HaveValue().Which[1];

        var result = await Persistence.LookupParties(
            organizationIdentifiers: [OrganizationWithChildrenIdentifier],
            personIdentifiers: [PersonIdentifier],
            userIds: [person1UserId, person2HistoricalUserId])
            .ToListAsync();

        result.Should().HaveCount(4);

        var testDataPers = result.Should().ContainSingle(static p => p.PartyUuid == PersonUuid).Which.Should().BeOfType<PersonRecord>().Which;
        var testDataOrg = result.Should().ContainSingle(static p => p.PartyUuid == OrganizationWithChildrenUuid).Which.Should().BeOfType<OrganizationRecord>().Which;
        var person1Result = result.Should().ContainSingle(p => p.PartyUuid == person1.PartyUuid).Which.Should().BeOfType<PersonRecord>().Which;
        var person2Result = result.Should().ContainSingle(p => p.PartyUuid == person2.PartyUuid).Which.Should().BeOfType<PersonRecord>().Which;

        person1Result.User.Should().HaveValue().Which.UserIds.Should().Be(ImmutableValueArray.Create(person1UserId));
        person2Result.User.Should().HaveValue().Which.UserIds.Should().Be(ImmutableValueArray.Create(person2UserId, person2HistoricalUserId));
    }

    [Fact]
    public async Task LookupParties_With_SubUnits_OrdersCorrectly()
    {
        var result = await Persistence.LookupParties(
            partyUuids: [
                Guid.Parse("b6368d0a-bce4-4798-8460-f4f86fc354c2"),
                Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8"),
            ],
            include: PartyFieldIncludes.Party | PartyFieldIncludes.SubUnits)
            .Cast<OrganizationRecord>()
            .ToListAsync();

        result.Should().HaveCount(6);

        result[0].PartyUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");
        result[0].ParentOrganizationUuid.Should().BeUnset();

        result[1].PartyUuid.Should().HaveValue().Which.Should().Be("4b28742a-5cd0-400e-a096-bd9817d12dca");
        result[1].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");

        result[2].PartyUuid.Should().HaveValue().Which.Should().Be("ad18578d-94cb-4774-8f37-5b24801c219b");
        result[2].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");

        result[3].PartyUuid.Should().HaveValue().Which.Should().Be("ec09feda-5dba-4b84-ad0b-f7886e6082cd");
        result[3].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");

        result[4].PartyUuid.Should().HaveValue().Which.Should().Be("b6368d0a-bce4-4798-8460-f4f86fc354c2");
        result[4].ParentOrganizationUuid.Should().BeUnset();

        result[5].PartyUuid.Should().HaveValue().Which.Should().Be("08cb91ff-75a4-45a4-b141-3c6be1bf8728");
        result[5].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("b6368d0a-bce4-4798-8460-f4f86fc354c2");
    }

    [Fact]
    public async Task LookupParties_Shared_SubUnit()
    {
        var child = await UoW.CreateOrg(unitType: "BEDR");
        var parent1 = await UoW.CreateOrg(unitType: "AS");
        var parent2 = await UoW.CreateOrg(unitType: "AS");

        await UoW.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: child.PartyUuid.Value, to: parent1.PartyUuid.Value);
        await UoW.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: child.PartyUuid.Value, to: parent2.PartyUuid.Value);

        var result = await Persistence.LookupParties(
            partyUuids: [parent1.PartyUuid.Value, parent2.PartyUuid.Value],
            include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization | PartyFieldIncludes.SubUnits)
            .Cast<OrganizationRecord>()
            .ToListAsync();

        result.Should().HaveCount(4);

        List<OrganizationRecord> parents = [result[0], result[2]];
        parents.Sort(static (a, b) => a.VersionId.Value.CompareTo(b.VersionId.Value));

        result[0].PartyUuid.Should().Be(parents[0].PartyUuid);
        result[0].ParentOrganizationUuid.Should().BeUnset();

        result[1].PartyUuid.Should().Be(child.PartyUuid);
        result[1].ParentOrganizationUuid.Should().Be(parents[0].PartyUuid);

        result[2].PartyUuid.Should().Be(parents[1].PartyUuid);
        result[2].ParentOrganizationUuid.Should().BeUnset();

        result[3].PartyUuid.Should().Be(child.PartyUuid);
        result[3].ParentOrganizationUuid.Should().Be(parents[1].PartyUuid);
    }

    [Fact]
    public async Task GetRolesFromNonExistingParty_ReturnsEmpty()
    {
        var partyUuid = Guid.Empty;

        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(partyUuid).ToListAsync();

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRolesToNonExistingParty_ReturnsEmpty()
    {
        var partyUuid = Guid.Empty;

        var roles = await Persistence.GetExternalRoleAssignmentsToParty(partyUuid).ToListAsync();

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRolesFromParty_ReturnsRoles()
    {
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(ChildOrganizationUuid).ToListAsync();

        var role = roles.Should().ContainSingle().Which;

        using var scope = new AssertionScope();
        role.Source.Should().Be(ExternalRoleSource.CentralCoordinatingRegister);
        role.Identifier.Should().Be("hovedenhet");
        role.FromParty.Should().Be(ChildOrganizationUuid);
        role.ToParty.Should().Be(OrganizationWithChildrenUuid);

        role.Name.Should().BeUnset();
        role.Description.Should().BeUnset();
    }

    [Fact]
    public async Task GetRoles_CanInclude_RoleDefinitions()
    {
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(ChildOrganizationUuid, include: PartyExternalRoleAssignmentFieldIncludes.RoleAssignment | PartyExternalRoleAssignmentFieldIncludes.RoleDefinition).ToListAsync();

        var role = roles.Should().ContainSingle().Which;

        using var scope = new AssertionScope();
        role.Source.Should().Be(ExternalRoleSource.CentralCoordinatingRegister);
        role.Identifier.Should().Be("hovedenhet");
        role.FromParty.Should().Be(ChildOrganizationUuid);
        role.ToParty.Should().Be(OrganizationWithChildrenUuid);

        IReadOnlyDictionary<LangCode, string> name = role.Name.Should().HaveValue().Which;
        IReadOnlyDictionary<LangCode, string> description = role.Description.Should().HaveValue().Which;

        name.Should().ContainKey(LangCode.En).WhoseValue.Should().Be("Has as the registration entity");
        name.Should().ContainKey(LangCode.Nb).WhoseValue.Should().Be("Har som registreringsenhet");
        name.Should().ContainKey(LangCode.Nn).WhoseValue.Should().Be("Har som registreringseininga");

        description.Should().ContainKey(LangCode.En).WhoseValue.Should().Be("Has as the registration entity");
        description.Should().ContainKey(LangCode.Nb).WhoseValue.Should().Be("Har som registreringsenhet");
        description.Should().ContainKey(LangCode.Nn).WhoseValue.Should().Be("Har som registreringseininga");
    }

    [Fact]
    public async Task GetRolesToParty_ReturnsRoles()
    {
        var party = Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8");
        var roles = await Persistence.GetExternalRoleAssignmentsToParty(party).ToListAsync();

        roles.Should().HaveCount(3);

        roles.Should().AllSatisfy(role =>
        {
            using var scope = new AssertionScope();
            role.Source.Should().Be(ExternalRoleSource.CentralCoordinatingRegister);
            role.Identifier.Should().Be("hovedenhet");
            role.FromParty.Should().HaveValue();
            role.ToParty.Should().Be(party);

            role.Name.Should().BeUnset();
            role.Description.Should().BeUnset();
        });
    }

    [Fact]
    public async Task GetRolesToParty_FilterByRole()
    {
        var role1 = await UoW.CreateFakeRoleDefinition(ExternalRoleSource.CentralCoordinatingRegister, "fake");
        var role2 = await UoW.CreateFakeRoleDefinition(ExternalRoleSource.CentralCoordinatingRegister, "fake-other");

        var org1 = await UoW.CreateOrg();
        var org2 = await UoW.CreateOrg();
        var org3 = await UoW.CreateOrg();
        var org4 = await UoW.CreateOrg();

        await Persistence.UpsertExternalRolesFromPartyBySource(
            commandId: Guid.CreateVersion7(),
            partyUuid: org2.PartyUuid.Value,
            roleSource: ExternalRoleSource.CentralCoordinatingRegister,
            assignments: [
                new(role1.Identifier, org1.PartyUuid.Value),
                new(role2.Identifier, org1.PartyUuid.Value),
            ]);

        await Persistence.UpsertExternalRolesFromPartyBySource(
            commandId: Guid.CreateVersion7(),
            partyUuid: org3.PartyUuid.Value,
            roleSource: ExternalRoleSource.CentralCoordinatingRegister,
            assignments: [
                new(role1.Identifier, org1.PartyUuid.Value),
            ]);

        await Persistence.UpsertExternalRolesFromPartyBySource(
            commandId: Guid.CreateVersion7(),
            partyUuid: org4.PartyUuid.Value,
            roleSource: ExternalRoleSource.CentralCoordinatingRegister,
            assignments: [
                new(role2.Identifier, org1.PartyUuid.Value),
            ]);

        var roles = await Persistence.GetExternalRoleAssignmentsToParty(
            partyUuid: org1.PartyUuid.Value,
            role: new(role1.Source, role1.Identifier))
            .ToListAsync();

        roles.Should().HaveCount(2);
        roles.Should().ContainSingle(r => r.FromParty.Value == org2.PartyUuid.Value);
        roles.Should().ContainSingle(r => r.FromParty.Value == org3.PartyUuid.Value);
        roles.Should().AllSatisfy(r => r.ToParty.Should().Be(org1.PartyUuid.Value));
    }

    [Theory]
    [InlineData(PartyFieldIncludes.Party)]
    [InlineData(PartyFieldIncludes.Identifiers)]
    [InlineData(PartyFieldIncludes.Party | PartyFieldIncludes.Organization | PartyFieldIncludes.Person)]
    public async Task GetPartyStream(PartyFieldIncludes includes)
    {
        var items = await Persistence.GetPartyStream(0, 100, includes).ToListAsync();

        items.Should().HaveCount(100);
    }

    #region Upsert Org

    [Fact]
    public async Task UpsertParty_Org_Inserts_New_Org()
    {
        var id = await UoW.GetNextPartyId();
        var orgNo = await UoW.GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            UnitStatus = "N",
            UnitType = "AS",
            TelephoneNumber = null,
            MobileNumber = null,
            FaxNumber = null,
            EmailAddress = null,
            InternetAddress = null,
            MailingAddress = null,
            BusinessAddress = null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        var inserted = result.Should().HaveValue().Which.Should().BeOfType<OrganizationRecord>().Which;
        inserted.Should().BeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_Updates_Name_And_Updated_And_OrgProps()
    {
        var id = await UoW.GetNextPartyId();
        var orgNo = await UoW.GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            UnitStatus = "N",
            UnitType = "AS",
            TelephoneNumber = null,
            MobileNumber = null,
            FaxNumber = null,
            EmailAddress = null,
            InternetAddress = null,
            MailingAddress = null,
            BusinessAddress = null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            UnitStatus = "U",
            UnitType = "hovedenhet",
            TelephoneNumber = "tel",
            MobileNumber = "mob",
            FaxNumber = "fax",
            EmailAddress = "email",
            InternetAddress = "internet",
            MailingAddress = new MailingAddressRecord { Address = "mailing", City = "mailing city", PostalCode = "0123" },
            BusinessAddress = new MailingAddressRecord { Address = "business", City = "business city", PostalCode = "0123" },
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.Should().BeOfType<OrganizationRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_Cannot_Update_Id()
    {
        var id = await UoW.GetNextPartyId();
        var orgNo = await UoW.GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            UnitStatus = "N",
            UnitType = "AS",
            TelephoneNumber = null,
            MobileNumber = null,
            FaxNumber = null,
            EmailAddress = null,
            InternetAddress = null,
            MailingAddress = null,
            BusinessAddress = null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            PartyId = id + 1,
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_Cannot_Update_OrgNr()
    {
        var id = await UoW.GetNextPartyId();
        var orgNo = await UoW.GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            UnitStatus = "N",
            UnitType = "AS",
            TelephoneNumber = null,
            MobileNumber = null,
            FaxNumber = null,
            EmailAddress = null,
            InternetAddress = null,
            MailingAddress = null,
            BusinessAddress = null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            OrganizationIdentifier = await UoW.GetNewOrgNumber(),
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_CannotInsert_WithSamePartyId()
    {
        var id = await UoW.GetNextPartyId();
        var orgNo = await UoW.GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            UnitStatus = "N",
            UnitType = "AS",
            TelephoneNumber = null,
            MobileNumber = null,
            FaxNumber = null,
            EmailAddress = null,
            InternetAddress = null,
            MailingAddress = null,
            BusinessAddress = null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var uuid2 = Guid.NewGuid();
        var toInsert2 = new OrganizationRecord
        {
            PartyUuid = uuid2,
            PartyId = id,
            DisplayName = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = await UoW.GetNewOrgNumber(),
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            UnitStatus = "N",
            UnitType = "AS",
            TelephoneNumber = null,
            MobileNumber = null,
            FaxNumber = null,
            EmailAddress = null,
            InternetAddress = null,
            MailingAddress = null,
            BusinessAddress = null,
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toInsert2);
        result.Should().BeProblem(Problems.PartyConflict.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid2, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).ToListAsync();
        fromDb.Should().BeEmpty();
    }

    #endregion

    #region Upsert Person

    [Fact]
    public async Task UpsertParty_Person_Inserts_New_Person()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        var inserted = result.Should().HaveValue().Which.Should().BeOfType<PersonRecord>().Which;
        inserted.Should().BeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_Updates_Name_And_Updated_And_PersonProps()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test Updated",
            MiddleName = "Mid Updated",
            LastName = "Testson Updated",
            ShortName = "TESTSON Updated Test Mid",
            Address = new StreetAddressRecord
            {
                MunicipalName = "mn",
                MunicipalNumber = "00",
                HouseNumber = "50",
                HouseLetter = "L",
                City = "s",
                PostalCode = "pc",
                StreetName = "sn",
            },
            MailingAddress = new MailingAddressRecord { Address = "mailing", City = "mailing city", PostalCode = "mailing postal code" },
            DateOfBirth = birthDate.AddDays(10),
            DateOfDeath = birthDate.AddDays(30),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.Should().BeOfType<PersonRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_Cannot_Update_Id()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            PartyId = id + 1,
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_Cannot_Update_PersonIdentifier()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            PersonIdentifier = await UoW.GetNewPersonIdentifier(birthDate, isDNumber),
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_CannotInsert_WithSamePartyId()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var uuid2 = Guid.NewGuid();
        var toInsert2 = new PersonRecord
        {
            PartyUuid = uuid2,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toInsert2);
        result.Should().BeProblem(Problems.PartyConflict.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid2, PartyFieldIncludes.Party | PartyFieldIncludes.Person).ToListAsync();
        fromDb.Should().BeEmpty();
    }

    #endregion

    #region Upsert Self-Identified User

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_Inserts_New_SelfIdentifiedUser()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test SI User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        var inserted = result.Should().HaveValue().Which.Should().BeOfType<SelfIdentifiedUserRecord>().Which;
        inserted.Should().BeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_Updates_Name_And_Updated()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test SI User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.Should().BeOfType<SelfIdentifiedUserRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpsertParty_SelfIdentifiedUser_Can_Retain_IsDeleted(bool isDeleted)
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test SI User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? TimeProvider.GetUtcNow() : FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            IsDeleted = FieldValue.Unset,
            DeletedAt = FieldValue.Unset,
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue();

        var expected = toUpdate with
        {
            IsDeleted = toInsert.IsDeleted, // IsDeleted should not change
            DeletedAt = toInsert.DeletedAt, // DeletedAt should not change
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.Should().BeOfType<SelfIdentifiedUserRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_IsDeleted_DefaultsFalse()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test SI User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = FieldValue.Unset,
            DeletedAt = FieldValue.Unset,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        var expected = toInsert with
        {
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
        };

        var updated = result.Value.Should().BeOfType<SelfIdentifiedUserRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    #endregion

    #region Upsert System User

    [Theory]
    [InlineData(SystemUserRecordType.Standard)]
    [InlineData(SystemUserRecordType.Agent)]
    public async Task UpsertParty_SystemUser_Inserts_New_SystemUser(SystemUserRecordType type)
    {
        var org = await UoW.CreateOrg();
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            DisplayName = "Test System User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = org.PartyUuid,
            SystemUserType = type,
        };

        var result = await Persistence.UpsertParty(toInsert);
        var inserted = result.Should().HaveValue().Which.Should().BeOfType<SystemUserRecord>().Which;
        inserted.Should().BeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SystemUser).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SystemUser_Updates_Name_And_Updated()
    {
        var org = await UoW.CreateOrg();
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            DisplayName = "Test System User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = org.PartyUuid,
            SystemUserType = SystemUserRecordType.Standard,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.Should().BeOfType<SystemUserRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SystemUser).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SystemUser_CanKeepOwner()
    {
        var org = await UoW.CreateOrg();
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            DisplayName = "Test System User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = org.PartyUuid,
            SystemUserType = SystemUserRecordType.Standard,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            OwnerUuid = FieldValue.Unset,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue();

        var expected = toUpdate with
        {
            OwnerUuid = toInsert.OwnerUuid, // owner should not change
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.Should().BeOfType<SystemUserRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SystemUser).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SystemUser_Cannot_Update_Owner()
    {
        var org1 = await UoW.CreateOrg();
        var org2 = await UoW.CreateOrg();
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            DisplayName = "Test System User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = org1.PartyUuid,
            SystemUserType = SystemUserRecordType.Agent,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            OwnerUuid = org2.PartyUuid,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate);
        var error = result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode).Which;
        error.Extensions.Should().ContainKey("column")
            .WhoseValue.Should().Be("owner");
    }

    #endregion

    #region Upsert Enterprise User

    [Fact]
    public async Task UpsertParty_EnterpriseUser_Inserts_New_EnterpriseUser()
    {
        var org = await UoW.CreateOrg();
        var uuid = Guid.NewGuid();

        var toInsert = new EnterpriseUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            DisplayName = "Test SI User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = org.PartyUuid,
        };

        var result = await Persistence.UpsertParty(toInsert);
        var inserted = result.Should().HaveValue().Which.Should().BeOfType<EnterpriseUserRecord>().Which;
        inserted.Should().BeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_EnterpriseUser_Updates_Name_And_Updated()
    {
        var org = await UoW.CreateOrg();
        var uuid = Guid.NewGuid();

        var toInsert = new EnterpriseUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            DisplayName = "Test SI User",
            PersonIdentifier = null,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = org.PartyUuid,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.Should().BeOfType<EnterpriseUserRecord>().Which;
        updated.Should().BeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    #endregion

    #region Upsert Role-Assigments

    [Fact]
    public async Task UpsertExternalRolesFromPartyBySource()
    {
        var added = ExternalRoleAssignmentEvent.EventType.Added;
        var removed = ExternalRoleAssignmentEvent.EventType.Removed;
        var source = ExternalRoleSource.CentralCoordinatingRegister;

        await UoW.CreateFakeRoleDefinitions();

        var party1 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000001"))).PartyUuid.Value;
        var party2 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000002"))).PartyUuid.Value;
        var party3 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000003"))).PartyUuid.Value;
        var party4 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000004"))).PartyUuid.Value;

        // assign empty to already empty
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            source,
            [],
            []);

        // assign new roles
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            source,
            [
                new("hovedenhet", party2),
                new("ikke-naeringsdrivende-hovedenhet", party2),
                new("styremedlem", party2),
            ],
            [
                new(added, "hovedenhet", party2),
                new(added, "ikke-naeringsdrivende-hovedenhet", party2),
                new(added, "styremedlem", party2),
            ]);

        // remove 1 role
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            source,
            [
                new("hovedenhet", party2),
                new("styremedlem", party2),
            ],
            [
                new(removed, "ikke-naeringsdrivende-hovedenhet", party2),
            ]);

        // replace 1 role
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            source,
            [
                new("hovedenhet", party2),
                new("ikke-naeringsdrivende-hovedenhet", party2),
            ],
            [
                new(added, "ikke-naeringsdrivende-hovedenhet", party2),
                new(removed, "styremedlem", party2),
            ]);

        // replace all roles
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            source,
            [
                new("styremedlem", party2),
            ],
            [
                new(added, "styremedlem", party2),
                new(removed, "hovedenhet", party2),
                new(removed, "ikke-naeringsdrivende-hovedenhet", party2),
            ]);

        // remove all roles
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            source,
            [],
            [
                new(removed, "styremedlem", party2),
            ]);

        // add roles from ccr
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            ExternalRoleSource.CentralCoordinatingRegister,
            [
                new("fake-01", party2),
                new("fake-02", party2),
                new("fake-03", party2),
                new("fake-04", party2),
            ],
            [
                new(added, "fake-01", party2),
                new(added, "fake-02", party2),
                new(added, "fake-03", party2),
                new(added, "fake-04", party2),
            ]);

        // add roles from npr
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            ExternalRoleSource.NationalPopulationRegister,
            [
                new("fake-01", party2),
                new("fake-02", party2),
                new("fake-03", party2),
                new("fake-04", party2),
            ],
            [
                new(added, "fake-01", party2),
                new(added, "fake-02", party2),
                new(added, "fake-03", party2),
                new(added, "fake-04", party2),
            ]);

        // replace roles from ccr
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            ExternalRoleSource.CentralCoordinatingRegister,
            [
                new("fake-03", party2),
                new("fake-04", party2),
                new("fake-05", party2),
                new("fake-06", party2),
            ],
            [
                new(removed, "fake-01", party2),
                new(removed, "fake-02", party2),
                new(added, "fake-05", party2),
                new(added, "fake-06", party2),
            ]);

        // replace roles from npr
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            ExternalRoleSource.NationalPopulationRegister,
            [
                new("fake-03", party2),
                new("fake-04", party2),
                new("fake-05", party2),
                new("fake-06", party2),
            ],
            [
                new(removed, "fake-01", party2),
                new(removed, "fake-02", party2),
                new(added, "fake-05", party2),
                new(added, "fake-06", party2),
            ]);

        // add same role to multiple parties
        await CheckUpsertExternalRolesFromPartyBySource(
            party1,
            ExternalRoleSource.CentralCoordinatingRegister,
            [
                new("fake-01", party3),
                new("fake-01", party4),
                new("fake-02", party3),
                new("fake-02", party4),
            ],
            [
                new(added, "fake-01", party3),
                new(added, "fake-01", party4),
                new(added, "fake-02", party3),
                new(added, "fake-02", party4),
                new(removed, "fake-03", party2),
                new(removed, "fake-04", party2),
                new(removed, "fake-05", party2),
                new(removed, "fake-06", party2),
            ]);

        // check final roles
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(party1).ToListAsync();
        roles.Should().HaveCount(8);

        roles.Should().ContainSingle(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-03", party2));
        roles.Should().ContainSingle(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-04", party2));
        roles.Should().ContainSingle(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-05", party2));
        roles.Should().ContainSingle(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-06", party2));

        roles.Should().ContainSingle(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-01", party3));
        roles.Should().ContainSingle(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-01", party4));
        roles.Should().ContainSingle(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-02", party3));
        roles.Should().ContainSingle(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-02", party4));

        static Expression<Func<PartyExternalRoleAssignmentRecord, bool>> Matches(ExternalRoleSource source, string identifier, Guid toParty)
            => r => r.Source == source && r.Identifier == identifier && r.ToParty == toParty;
    }

    private async Task CheckUpsertExternalRolesFromPartyBySource(
        Guid fromParty,
        ExternalRoleSource source,
        IReadOnlyList<IPartyExternalRolePersistence.UpsertExternalRoleAssignment> assignments,
        IReadOnlyList<CheckUpsertExternalRolesFromPartyBySourceExpectedEvent> expectedEvents)
    {
        var cmdId = Guid.CreateVersion7();
        var firstTryEvents = await Persistence
            .UpsertExternalRolesFromPartyBySource(
                cmdId,
                fromParty,
                source,
                assignments)
            .Select(static e => new CheckUpsertExternalRolesFromPartyBySourceExpectedEvent(e.Type, e.RoleIdentifier, e.ToParty))
            .ToListAsync();

        firstTryEvents.Should().HaveCount(expectedEvents.Count);
        foreach (var expected in expectedEvents)
        {
            firstTryEvents.Should().Contain(expected);
        }

        // idempotency check
        var secondTryEvents = await Persistence
            .UpsertExternalRolesFromPartyBySource(
                cmdId,
                fromParty,
                source,
                assignments)
            .Select(static e => new CheckUpsertExternalRolesFromPartyBySourceExpectedEvent(e.Type, e.RoleIdentifier, e.ToParty))
            .ToListAsync();

        secondTryEvents.Should().HaveCount(expectedEvents.Count);
        foreach (var expected in expectedEvents)
        {
            secondTryEvents.Should().Contain(expected);
        }

        // check that the roles are actually assigned
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(fromParty).Where(r => r.Source == source).ToListAsync();
        roles.Should().HaveCount(assignments.Count);
        foreach (var assignment in assignments)
        {
            roles.Should().ContainSingle(r => r.Identifier == assignment.RoleIdentifier && r.ToParty == assignment.ToParty);
        }
    }

    private record CheckUpsertExternalRolesFromPartyBySourceExpectedEvent(ExternalRoleAssignmentEvent.EventType Type, string Identifier, Guid ToParty);

    #endregion

    #region Sequence Transaction handling

    [Fact]
    public async Task Sequence_Transaction_Handling()
    {
        var dataSource = GetRequiredService<NpgsqlDataSource>();
        await using var noTxConn = await dataSource.OpenConnectionAsync();
        await using var maxSafeCmd = noTxConn.CreateCommand();
        maxSafeCmd.CommandText = /*strpsql*/"SELECT register.tx_max_safeval('register.party_version_id_seq')";
        await maxSafeCmd.PrepareAsync();

        await using var uow1 = await GetRequiredService<IUnitOfWorkManager>().CreateAsync(activityName: "uow1");
        await using var uow2 = await GetRequiredService<IUnitOfWorkManager>().CreateAsync(activityName: "uow2");

        var tx1Conn = uow1.GetRequiredService<NpgsqlConnection>();
        var tx2Conn = uow2.GetRequiredService<NpgsqlConnection>();

        Assert.Equal(9223372036854775807UL, await GetVisible());
        
        var val1 = await NextVal(tx1Conn);
        var val2 = await NextVal(tx2Conn);
        val1.Should().BeLessThan(val2);

        (await GetVisible()).Should().Be(val1 - 1);

        await uow2.CommitAsync();
        (await GetVisible()).Should().Be(val1 - 1);

        await uow1.CommitAsync();
        Assert.Equal(9223372036854775807UL, await GetVisible());

        async Task<ulong> GetVisible()
        {
            await using var reader = await maxSafeCmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow);
            Assert.True(await reader.ReadAsync());

            var result = await reader.GetFieldValueAsync<long>(0);
            return (ulong)result;
        }

        async Task<ulong> NextVal(NpgsqlConnection conn)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = /*strpsql*/"SELECT register.tx_nextval('register.party_version_id_seq')";
            await cmd.PrepareAsync();

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow);
            Assert.True(await reader.ReadAsync());

            var result = await reader.GetFieldValueAsync<long>(0);
            return (ulong)result;
        }
    }

    #endregion

    #region UserId handling

    [Fact]
    public async Task Person_CanBeUpdated_WithActiveUserId()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue().Which.User.Should().BeUnset();

        await NewTransaction();
        var userIds = await TestDataGenerator.GetNextUserIds();
        var toUpdate = toInsert with 
        {
            User = new PartyUserRecord(userId: userIds[0], username: FieldValue.Unset, userIds: userIds.ToImmutableValueArray()),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().HaveValue()
            .Which.User.Should().HaveValue()
            .Which.UserIds.Should().HaveValue()
            .Which.Should().ContainSingle()
            .Which.Should().Be(userIds[0]);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithActiveUserId_And_Username()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue().Which.User.Should().BeUnset();

        await NewTransaction();
        var userIds = await TestDataGenerator.GetNextUserIds();
        var username = $"user_{userIds[0]}";
        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: userIds[0], username: username, userIds: userIds.ToImmutableValueArray()),
        };

        result = await Persistence.UpsertParty(toUpdate);
        var user = result.Should().HaveValue()
            .Which.User.Should().HaveValue().Which;

        user.UserId.Should().Be(userIds[0]);
        user.UserIds.Should().HaveValue()
            .Which.Should().ContainSingle()
            .Which.Should().Be(userIds[0]);
        user.Username.Should().Be(username);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithHistoricalUserIds()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue().Which.User.Should().BeUnset();

        await NewTransaction();
        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate);
        var userIds = result.Should().HaveValue()
            .Which.User.Should().HaveValue()
            .Which.UserIds.Should().HaveValue()
            .Which;

        userIds.Should().HaveCount(3);
        userIds.Should().HaveElementAt(0, 10);
        userIds.Should().HaveElementAt(1, 5);
        userIds.Should().HaveElementAt(2, 2);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithHistoricalUserIds_Only()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue().Which.User.Should().BeUnset();

        await NewTransaction();
        var user = new PartyUserRecord(userId: 10U, username: FieldValue.Null, userIds: ImmutableValueArray.Create(10U, 2U, 5U));

        var userResult = await Persistence.UpsertPartyUser(uuid, user);
        var userIds = userResult.Should().HaveValue()
            .Which.UserIds.Should().HaveValue()
            .Which;

        userIds.Should().HaveCount(3);
        userIds.Should().HaveElementAt(0, 10);
        userIds.Should().HaveElementAt(1, 5);
        userIds.Should().HaveElementAt(2, 2);
    }

    [Fact]
    public async Task Person_CanBeInserted_WithHistoricalUserIds()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        var userIds = result.Should().HaveValue()
            .Which.User.Should().HaveValue()
            .Which.UserIds.Should().HaveValue()
            .Which;

        userIds.Should().HaveCount(3);
        userIds.Should().HaveElementAt(0, 10);
        userIds.Should().HaveElementAt(1, 5);
        userIds.Should().HaveElementAt(2, 2);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithoutTouchingUserIds()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        await NewTransaction();
        var toUpdate = toInsert with
        {
            User = FieldValue.Unset,
            DisplayName = "updated"
        };

        result = await Persistence.UpsertParty(toUpdate);
        var person = result.Should().HaveValue().Which.Should().BeOfType<PersonRecord>().Which;

        person.User.Should().BeUnset();

        await NewTransaction();
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT user_id, is_active FROM register.user WHERE uuid = @partyId ORDER BY user_id DESC";

        cmd.Parameters.Add<Guid>("partyId", NpgsqlTypes.NpgsqlDbType.Uuid).TypedValue = uuid;

        await using var reader = await cmd.ExecuteReaderAsync();
        
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt64("user_id").Should().Be(10);
        reader.GetBoolean("is_active").Should().BeTrue();

        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt64("user_id").Should().Be(5);
        reader.GetBoolean("is_active").Should().BeFalse();

        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt64("user_id").Should().Be(2);
        reader.GetBoolean("is_active").Should().BeFalse();

        (await reader.ReadAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Person_Cannot_UpdateActiveUserId()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 12U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(12U, 10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate);
        var problem = result.Should().BeProblem().Which;
        problem.ErrorCode.Should().Be(Problems.InvalidPartyUpdate.ErrorCode);
        problem.Extensions.Should().ContainKey("column").WhoseValue.Should().Be("user_id");
    }

    [Fact]
    public async Task Person_Can_UpdateActiveUsername()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = new PartyUserRecord(userId: 10U, username: "user1", userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: "user2", userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate);
        var user = result.Should().HaveValue().Which.User.Should().HaveValue().Which;
        user.UserId.Should().Be(10U);
        user.Username.Should().Be("user2");
    }

    [Fact]
    public async Task Person_Can_UnsetActiveUsername()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = new PartyUserRecord(userId: 10U, username: "user1", userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Null, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate);
        var user = result.Should().HaveValue().Which.User.Should().HaveValue().Which;
        user.UserId.Should().Be(10U);
        user.Username.Should().BeNull();
    }

    [Fact]
    public async Task Person_Can_RetainActiveUsername()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = new PartyUserRecord(userId: 10U, username: "user1", userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        result.Should().HaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate);
        var user = result.Should().HaveValue().Which.User.Should().HaveValue().Which;
        user.UserId.Should().Be(10U);
        user.Username.Should().Be("user1");
    }

    [Fact]
    public async Task UpsertUserRecord_CanCreate_New_UserRecord()
    {
        var person = await UoW.CreatePerson(user: FieldValue.Null);

        var userIds = await TestDataGenerator.GetNextUserIds();
        await Persistence.UpsertUserRecord(person.PartyUuid.Value, userIds[0], "test-user", isActive: true);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User).FirstOrDefaultAsync();
        updated.Should().NotBeNull();
        updated.User.Should().HaveValue();
        updated.User.Value!.UserId.Should().Be(userIds[0]);
        updated.User.Value!.Username.Should().Be("test-user");
    }

    [Fact]
    public async Task UpsertUserRecord_CanUpdate_ExistingRecord()
    {
        var ids = await TestDataGenerator.GetNextUserIds();
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[0], "test-user-name", ImmutableValueArray.ToImmutableValueArray(ids)));

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[0], "updated-user-name", isActive: true);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User).FirstOrDefaultAsync();
        updated.Should().NotBeNull();
        updated.User.Should().HaveValue();
        updated.User.Value!.UserId.Should().Be(ids[0]);
        updated.User.Value!.Username.Should().Be("updated-user-name");
    }

    [Fact]
    public async Task UpsertUserRecord_CanDeactivate_User()
    {
        var ids = await TestDataGenerator.GetNextUserIds();
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[0], "test-user-name", ImmutableValueArray.ToImmutableValueArray(ids)));

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[0], FieldValue.Unset, isActive: false);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User).FirstOrDefaultAsync();
        updated.Should().NotBeNull();
        updated.User.Should().BeNull();
        updated.VersionId.Should().HaveValue().Which.Should().BeGreaterThan(person.VersionId.Value);
    }

    [Fact]
    public async Task UpsertUserRecord_Inactive_DoesNotUpdatePartyVersionId()
    {
        var ids = await TestDataGenerator.GetNextUserIds(count: 2);
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[1], "test-user-name", ImmutableValueArray.Create(ids[1])));

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[0], "old-user-name", isActive: false);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User).FirstOrDefaultAsync();
        updated.Should().NotBeNull();
        updated.VersionId.Should().HaveValue().Which.Should().Be(person.VersionId.Value);
    }

    [Fact]
    public async Task UpsertUserRecord_Active_UpdatesPartyVersionId()
    {
        var ids = await TestDataGenerator.GetNextUserIds(count: 2);
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[1], "test-user-name", ImmutableValueArray.Create(ids[1])));

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[1], "old-user-name", isActive: true);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User).FirstOrDefaultAsync();
        updated.Should().NotBeNull();
        updated.VersionId.Should().HaveValue().Which.Should().BeGreaterThan(person.VersionId.Value);
    }

    #endregion

    #region AsyncSingleton

    [Fact]
    public async Task AsyncSingleton_YieldSingleParty()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var party = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = new PartyUserRecord(userId: 10U, username: "user1", userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var singleton = new PostgreSqlPartyPersistence.UpsertPartyQuery.AsyncSingleton(party);
        var list = await singleton.ToListAsync();

        list.Should().ContainSingle().Which.Should().BeSameAs(party);
    }

    #endregion
}
