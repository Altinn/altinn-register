using System.Data;
using System.Linq.Expressions;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Core.Utils;
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
    private readonly static int OrganizationWithChildrenId = 50056131;
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

        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        _unitOfWork = await uowManager.CreateAsync(activityName: "test");
        _connection = _unitOfWork.GetRequiredService<NpgsqlConnection>();
        _persistence = _unitOfWork.GetRequiredService<PostgreSqlPartyPersistence>();
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
        var partyId = 0;
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
        party.PartyType.Should().Be(PartyType.Organization);
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
        party.PartyType.Should().Be(PartyType.Organization);
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
    public async Task GetPartyByOrganizationIdentifier_CanGet_OrganizationData()
    {
        var result = await Persistence.GetOrganizationByIdentifier(OrganizationWithChildrenIdentifier, include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<OrganizationRecord>().Which;

        using var scope = new AssertionScope();
        party.ParentOrganizationUuid.Should().BeUnset();

        party.PartyUuid.Should().Be(OrganizationWithChildrenUuid);
        party.PartyId.Should().Be(OrganizationWithChildrenId);
        party.PartyType.Should().Be(PartyType.Organization);
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
        party.PartyType.Should().Be(PartyType.Person);
        party.DisplayName.Should().Be("SANNE BJØRKUM");
        party.PersonIdentifier.Should().Be(PersonIdentifier);
        party.OrganizationIdentifier.Should().BeNull();

        party.FirstName.Should().Be("SANNE");
        party.MiddleName.Should().BeNull();
        party.LastName.Should().Be("BJØRKUM");
        party.Address.Should().Be(new StreetAddress { PostalCode = "3230", City = "SANDEFJORD" });
        party.MailingAddress.Should().Be(new MailingAddress { Address = "Granholmveien 19", PostalCode = "3230", City = "SANDEFJORD" });
        party.DateOfBirth.Should().Be(new DateOnly(1862, 05, 01));
        party.DateOfDeath.Should().BeNull();
    }

    [Fact]
    public async Task GetPartyByPersonIdentifier_CanGet_PersonData()
    {
        var result = await Persistence.GetPartyByPersonIdentifier(PersonIdentifier, include: PartyFieldIncludes.Party | PartyFieldIncludes.Person).ToListAsync();

        var party = result.Should().ContainSingle().Which.Should().BeOfType<PersonRecord>().Which;

        using var scope = new AssertionScope();
        party.PartyUuid.Should().Be(PersonUuid);
        party.PartyId.Should().Be(50002129);
        party.PartyType.Should().Be(PartyType.Person);
        party.DisplayName.Should().Be("SANNE BJØRKUM");
        party.PersonIdentifier.Should().Be(PersonIdentifier);
        party.OrganizationIdentifier.Should().BeNull();

        party.FirstName.Should().Be("SANNE");
        party.MiddleName.Should().BeNull();
        party.LastName.Should().Be("BJØRKUM");
        party.Address.Should().Be(new StreetAddress { PostalCode = "3230", City = "SANDEFJORD" });
        party.MailingAddress.Should().Be(new MailingAddress { Address = "Granholmveien 19", PostalCode = "3230", City = "SANDEFJORD" });
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
            parent.PartyType.Should().Be(PartyType.Organization);
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
            child.PartyType.Should().Be(PartyType.Organization);
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
        party.PartyType.Should().Be(PartyType.Organization);
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
        var result = await Persistence.LookupParties(
            organizationIdentifiers: [OrganizationWithChildrenIdentifier],
            personIdentifiers: [PersonIdentifier])
            .ToListAsync();

        result.Should().HaveCount(2);

        var pers = result[0].Should().BeOfType<PersonRecord>().Which;
        var org = result[1].Should().BeOfType<OrganizationRecord>().Which;

        pers.PartyUuid.Should().Be(PersonUuid);
        org.PartyUuid.Should().Be(OrganizationWithChildrenUuid);
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

        result[0].PartyUuid.Should().HaveValue().Which.Should().Be("b6368d0a-bce4-4798-8460-f4f86fc354c2");
        result[0].ParentOrganizationUuid.Should().BeUnset();

        result[1].PartyUuid.Should().HaveValue().Which.Should().Be("08cb91ff-75a4-45a4-b141-3c6be1bf8728");
        result[1].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("b6368d0a-bce4-4798-8460-f4f86fc354c2");

        result[2].PartyUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");
        result[2].ParentOrganizationUuid.Should().BeUnset();

        result[3].PartyUuid.Should().HaveValue().Which.Should().Be("4b28742a-5cd0-400e-a096-bd9817d12dca");
        result[3].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");

        result[4].PartyUuid.Should().HaveValue().Which.Should().Be("ad18578d-94cb-4774-8f37-5b24801c219b");
        result[4].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");

        result[5].PartyUuid.Should().HaveValue().Which.Should().Be("ec09feda-5dba-4b84-ad0b-f7886e6082cd");
        result[5].ParentOrganizationUuid.Should().HaveValue().Which.Should().Be("e2081abd-a16f-4302-93b0-05aaa42023e8");
    }

    [Fact]
    public async Task LookupParties_Shared_SubUnit()
    {
        var child = await UoW.CreateOrg(unitType: "hovedenhet");
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

        List<Guid> parentIds = [result[0].PartyUuid.Value, result[2].PartyUuid.Value];
        parentIds.Sort();

        result[0].PartyUuid.Should().Be(parentIds[0]);
        result[0].ParentOrganizationUuid.Should().BeUnset();

        result[1].PartyUuid.Should().Be(child.PartyUuid);
        result[1].ParentOrganizationUuid.Should().Be(parentIds[0]);

        result[2].PartyUuid.Should().Be(parentIds[1]);
        result[2].ParentOrganizationUuid.Should().BeUnset();

        result[3].PartyUuid.Should().Be(child.PartyUuid);
        result[3].ParentOrganizationUuid.Should().Be(parentIds[1]);
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
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(ChildOrganizationUuid, PartyExternalRoleAssignmentFieldIncludes.RoleAssignment | PartyExternalRoleAssignmentFieldIncludes.RoleDefinition).ToListAsync();

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
            VersionId = FieldValue.Unset,
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
            VersionId = FieldValue.Unset,
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
            MailingAddress = new MailingAddress { Address = "mailing", City = "mailing city", PostalCode = "0123" },
            BusinessAddress = new MailingAddress { Address = "business", City = "business city", PostalCode = "0123" },
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
            VersionId = FieldValue.Unset,
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

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);
        
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
            VersionId = FieldValue.Unset,
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

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

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
            VersionId = FieldValue.Unset,
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
            VersionId = FieldValue.Unset,
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

        result = await Persistence.UpsertParty(toInsert2);
        result.Should().BeProblem(Problems.PartyConflict.ErrorCode);

        var fromDb = await Persistence.GetPartyById(uuid2, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).ToListAsync();
        fromDb.Should().BeEmpty();
    }

    #endregion

    #region Upsert Person

    [Fact]
    public async Task UpsertParty_Org_Inserts_New_Person()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
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
            VersionId = FieldValue.Unset,
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
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
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
            VersionId = FieldValue.Unset,
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
            Address = new StreetAddress
            {
                MunicipalName = "mn",
                MunicipalNumber = "00",
                HouseNumber = "50",
                HouseLetter = "L",
                City = "s",
                PostalCode = "pc",
                StreetName = "sn",
            },
            MailingAddress = new MailingAddress { Address = "mailing", City = "mailing city", PostalCode = "mailing postal code" },
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
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
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
            VersionId = FieldValue.Unset,
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

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_Cannot_Update_PersonIdentifier()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
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
            VersionId = FieldValue.Unset,
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

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_CannotInsert_WithSamePartyId()
    {
        var id = await UoW.GetNextPartyId();
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
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
            VersionId = FieldValue.Unset,
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
            VersionId = FieldValue.Unset,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        result = await Persistence.UpsertParty(toInsert2);
        result.Should().BeProblem(Problems.PartyConflict.ErrorCode);

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
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
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
            VersionId = FieldValue.Unset,
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
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
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
            VersionId = FieldValue.Unset,
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
}
