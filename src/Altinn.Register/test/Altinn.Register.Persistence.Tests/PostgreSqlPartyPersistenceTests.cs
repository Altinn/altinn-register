using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Core.Utils;
using Altinn.Register.TestUtils;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Latency;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence.Tests;

public class PostgreSqlPartyPersistenceTests
    : DatabaseTestBase
{
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

    [Fact]
    public void CanGet_IPartyPersistence()
    {
        var persistence = _unitOfWork!.GetPartyPersistence();
        persistence.Should().BeSameAs(Persistence);
    }

    [Fact]
    public void CanGet_IPartyRolePersistence()
    {
        var persistence = _unitOfWork!.GetRolePersistence();
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
        party.Name.Should().Be("MOEN OG BJØRNEVATN");
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
        party.Name.Should().Be("MOEN OG BJØRNEVATN");
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
        party.Name.Should().Be("MOEN OG BJØRNEVATN");
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
        party.Name.Should().Be("SANNE BJØRKUM");
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
        party.Name.Should().Be("SANNE BJØRKUM");
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
            parent.Name.Should().Be("MOEN OG BJØRNEVATN");
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
            child.Name.Should().Be("NERLANDSØY OG DYRANUT");
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
        party.Name.Should().Be("MOEN OG BJØRNEVATN");
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
        var child = await CreateOrg(unitType: "BEDR");
        var parent1 = await CreateOrg(unitType: "AS");
        var parent2 = await CreateOrg(unitType: "AS");

        await AddRole(PartySource.CentralCoordinatingRegister, "bedr", from: child.PartyUuid.Value, to: parent1.PartyUuid.Value);
        await AddRole(PartySource.CentralCoordinatingRegister, "bedr", from: child.PartyUuid.Value, to: parent2.PartyUuid.Value);

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

        var roles = await Persistence.GetRolesFromParty(partyUuid).ToListAsync();

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRolesToNonExistingParty_ReturnsEmpty()
    {
        var partyUuid = Guid.Empty;

        var roles = await Persistence.GetRolesToParty(partyUuid).ToListAsync();

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRolesFromParty_ReturnsRoles()
    {
        var roles = await Persistence.GetRolesFromParty(ChildOrganizationUuid).ToListAsync();

        var role = roles.Should().ContainSingle().Which;

        using var scope = new AssertionScope();
        role.Source.Should().Be(PartySource.CentralCoordinatingRegister);
        role.Identifier.Should().Be("bedr");
        role.FromParty.Should().Be(ChildOrganizationUuid);
        role.ToParty.Should().Be(OrganizationWithChildrenUuid);

        role.Name.Should().BeUnset();
        role.Description.Should().BeUnset();
    }

    [Fact]
    public async Task GetRoles_CanInclude_RoleDefinitions()
    {
        var roles = await Persistence.GetRolesFromParty(ChildOrganizationUuid, PartyRoleFieldIncludes.Role | PartyRoleFieldIncludes.RoleDefinition).ToListAsync();

        var role = roles.Should().ContainSingle().Which;

        using var scope = new AssertionScope();
        role.Source.Should().Be(PartySource.CentralCoordinatingRegister);
        role.Identifier.Should().Be("bedr");
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
        var roles = await Persistence.GetRolesToParty(party).ToListAsync();

        roles.Should().HaveCount(3);

        roles.Should().AllSatisfy(role =>
        {
            using var scope = new AssertionScope();
            role.Source.Should().Be(PartySource.CentralCoordinatingRegister);
            role.Identifier.Should().Be("bedr");
            role.FromParty.Should().HaveValue();
            role.ToParty.Should().Be(party);

            role.Name.Should().BeUnset();
            role.Description.Should().BeUnset();
        });
    }

    #region Upsert Org

    [Fact]
    public async Task UpsertParty_Org_Inserts_New_Org()
    {
        var id = await GetNextPartyId();
        var orgNo = await GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
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
        inserted.Should().BeEquivalentTo(toInsert);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert);
    }

    [Fact]
    public async Task UpsertParty_Org_Updates_Name_And_Updated_And_OrgProps()
    {
        var id = await GetNextPartyId();
        var orgNo = await GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
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
            Name = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            UnitStatus = "U",
            UnitType = "BEDR",
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
        updated.Should().BeEquivalentTo(expected);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task UpsertParty_Org_Cannot_Update_Id()
    {
        var id = await GetNextPartyId();
        var orgNo = await GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
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
        fromDb.Should().BeEquivalentTo(toInsert);
    }

    [Fact]
    public async Task UpsertParty_Org_Cannot_Update_OrgNr()
    {
        var id = await GetNextPartyId();
        var orgNo = await GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
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
            OrganizationIdentifier = await GetNewOrgNumber(),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert);
    }

    [Fact]
    public async Task UpsertParty_Org_CannotInsert_WithSamePartyId()
    {
        var id = await GetNextPartyId();
        var orgNo = await GetNewOrgNumber();
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
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
            Name = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = await GetNewOrgNumber(),
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
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
        var id = await GetNextPartyId();
        var birthDate = GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
        var personId = await GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert);
        var inserted = result.Should().HaveValue().Which.Should().BeOfType<PersonRecord>().Which;
        inserted.Should().BeEquivalentTo(toInsert);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert);
    }

    [Fact]
    public async Task UpsertParty_Person_Updates_Name_And_Updated_And_PersonProps()
    {
        var id = await GetNextPartyId();
        var birthDate = GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
        var personId = await GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
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
            Name = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test Updated",
            MiddleName = "Mid Updated",
            LastName = "Testson Updated",
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
        updated.Should().BeEquivalentTo(expected);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task UpsertParty_Person_Cannot_Update_Id()
    {
        var id = await GetNextPartyId();
        var birthDate = GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
        var personId = await GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
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
        fromDb.Should().BeEquivalentTo(toInsert);
    }

    [Fact]
    public async Task UpsertParty_Person_Cannot_Update_PersonIdentifier()
    {
        var id = await GetNextPartyId();
        var birthDate = GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
        var personId = await GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
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
            PersonIdentifier = await GetNewPersonIdentifier(birthDate, isDNumber),
        };

        result = await Persistence.UpsertParty(toUpdate);
        result.Should().BeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).SingleAsync();
        fromDb.Should().BeEquivalentTo(toInsert);
    }

    [Fact]
    public async Task UpsertParty_Person_CannotInsert_WithSamePartyId()
    {
        var id = await GetNextPartyId();
        var birthDate = GetRandomBirthDate();
        var isDNumber = Random.Shared.NextDouble() <= 0.1; // 10% chance of D-number
        var personId = await GetNewPersonIdentifier(birthDate, isDNumber);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            Name = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
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
            Name = "Test Mid Testson",
            PersonIdentifier = personId,
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
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

    private async Task<OrganizationIdentifier> GetNewOrgNumber()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT organization_identifier 
            FROM register.party 
            WHERE organization_identifier = @id
            """;

        var param = cmd.Parameters.Add<string>("id", NpgsqlDbType.Text);
        await cmd.PrepareAsync();

        OrganizationIdentifier id;
        do
        {
            id = GenerateOrganizationIdentifier();
        }
        while (await InUse(id));

        return id;

        async Task<bool> InUse(OrganizationIdentifier id)
        {
            param.TypedValue = id.ToString();

            await using var reader = await cmd.ExecuteReaderAsync();
            var exists = await reader.ReadAsync();
            return exists;
        }

        static OrganizationIdentifier GenerateOrganizationIdentifier()
        {
            Vector128<ushort> weights = Vector128.Create((ushort)3, 2, 7, 6, 5, 4, 3, 2);

            while (true)
            {
                // 8 digit random number
                var random = Random.Shared.Next(10_000_000, 99_999_999);
                Span<char> s = stackalloc char[9];
                Debug.Assert(random.TryFormat(s, out var written, provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 8);

                ReadOnlySpan<ushort> chars = MemoryMarshal.Cast<char, ushort>(s);

                Vector128<ushort> zeroDigit = Vector128.Create('0', '0', '0', '0', '0', '0', '0', '0');
                Vector128<ushort> charsVec = Vector128.Create(chars);

                var sum = Vector128.Sum((charsVec - zeroDigit) * weights);

                var ctrlDigit = 11 - (sum % 11);
                if (ctrlDigit == 11)
                {
                    ctrlDigit = 0;
                }

                if (ctrlDigit == 10)
                {
                    continue;
                }

                Debug.Assert(ctrlDigit is >= 0 and <= 9, $"ctrlDigit was {ctrlDigit}");
                s[8] = (char)('0' + ctrlDigit);

                return OrganizationIdentifier.Parse(new string(s));
            }
        }
    }

    private async Task<PersonIdentifier> GetNewPersonIdentifier(DateOnly birthDate, bool isDNumber)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT person_identifier 
            FROM register.party 
            WHERE person_identifier = @id
            """;

        var param = cmd.Parameters.Add<string>("id", NpgsqlDbType.Text);
        await cmd.PrepareAsync();

        PersonIdentifier id;
        do
        {
            id = GeneratePersonIdentifier(birthDate, isDNumber);
        }
        while (await InUse(id));

        return id;

        async Task<bool> InUse(PersonIdentifier id)
        {
            param.TypedValue = id.ToString();

            await using var reader = await cmd.ExecuteReaderAsync();
            var exists = await reader.ReadAsync();
            return exists;
        }

        static PersonIdentifier GeneratePersonIdentifier(DateOnly dateComp, bool isDNumber)
        {
            Vector256<ushort> k1weights = Vector256.Create((ushort)3, 7, 6, 1, 8, 9, 4, 5, 2, 0, 0, 0, 0, 0, 0, 0);
            Vector256<ushort> k2weights = Vector256.Create((ushort)5, 4, 3, 2, 7, 6, 5, 4, 3, 2, 0, 0, 0, 0, 0, 0);
            Span<ushort> k1_candidates = stackalloc ushort[4];

            var random = Random.Shared;

            var dayOffset = isDNumber ? 40 : 0;
            int written;

            while (true)
            {
                var individualNumber = random.Next(0, 1000);
                Span<char> s = stackalloc char[11];
                s.Fill('0');

                var day = dateComp.Day + dayOffset;
                var month = dateComp.Month;
                var year = dateComp.Year % 100;

                Debug.Assert(day.TryFormat(s, out written, "D2", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 2);

                Debug.Assert(month.TryFormat(s.Slice(2), out written, "D2", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 2);

                Debug.Assert(year.TryFormat(s.Slice(4), out written, "D2", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 2);

                Debug.Assert(individualNumber.TryFormat(s.Slice(6), out written, "D3", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 3);

                Vector256<ushort> digits = CreateVector(s);

                var k1c_base = (ushort)(Vector256.Sum(digits * k1weights) % 11);
                var k1c_1 = (ushort)((11 - k1c_base) % 11);
                var k1c_2 = (ushort)((12 - k1c_base) % 11);
                var k1c_3 = (ushort)((13 - k1c_base) % 11);
                var k1c_4 = (ushort)((14 - k1c_base) % 11);

                var idx = 0;
                AddIfValid(k1_candidates, ref idx, k1c_1);
                AddIfValid(k1_candidates, ref idx, k1c_2);
                AddIfValid(k1_candidates, ref idx, k1c_3);
                AddIfValid(k1_candidates, ref idx, k1c_4);

                var k1 = k1_candidates[random.Next(0, idx)];
                Debug.Assert(k1.TryFormat(s.Slice(9), out written, "D1", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 1);

                digits = CreateVector(s);
                var k2 = (ushort)((11 - (Vector256.Sum(digits * k2weights) % 11)) % 11);

                if (k2 == 10)
                {
                    continue;
                }

                Debug.Assert(k2.TryFormat(s.Slice(10), out written, "D1", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 1);

                if (!PersonIdentifier.TryParse(s, provider: null, out var result))
                {
                    Assert.Fail($"Generated illegal person identifier: {new string(s)}");
                }

                return result;
            }
        }

        static void AddIfValid(Span<ushort> candidates, ref int idx, ushort value)
        {
            if (value != 10)
            {
                candidates[idx++] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<ushort> CreateVector(ReadOnlySpan<char> s)
        {
            Debug.Assert(s.Length == 11);

            Span<ushort> c = stackalloc ushort[16];
            c.Clear(); // zero out the vector
            MemoryMarshal.Cast<char, ushort>(s).CopyTo(c);

            var chars = Vector256.Create<ushort>(c);
            var zeros = Vector256.Create('0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', 0, 0, 0, 0, 0);

            return chars - zeros;
        }
    }

    private static DateOnly GetRandomBirthDate()
    {
        var min = new DateOnly(1940, 01, 01);
        var maxExl = new DateOnly(2024, 01, 01);
        var value = Random.Shared.Next(min.DayNumber, maxExl.DayNumber);

        return DateOnly.FromDayNumber(value);
    }

    private async Task<int> GetNextPartyId()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT MAX(id) FROM register.party
            """;

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) + 1;
    }

    private async Task<OrganizationRecord> CreateOrg(
        FieldValue<Guid> uuid = default,
        FieldValue<int> id = default,
        FieldValue<string> name = default,
        FieldValue<OrganizationIdentifier> identifier = default,
        FieldValue<DateTimeOffset> createdAt = default,
        FieldValue<DateTimeOffset> modifiedAt = default,
        FieldValue<string> unitStatus = default,
        FieldValue<string> unitType = default,
        FieldValue<string> telephoneNumber = default,
        FieldValue<string> mobileNumber = default,
        FieldValue<string> faxNumber = default,
        FieldValue<string> emailAddress = default,
        FieldValue<string> internetAddress = default,
        FieldValue<MailingAddress> mailingAddress = default,
        FieldValue<MailingAddress> businessAddress = default)
    {
        if (!id.HasValue)
        {
            id = await GetNextPartyId();
        }

        if (!identifier.HasValue)
        {
            identifier = await GetNewOrgNumber();
        }

        var result = await Persistence.UpsertParty(new OrganizationRecord
        {
            PartyUuid = uuid.HasValue ? uuid.Value : Guid.NewGuid(),
            PartyId = id,
            Name = name.HasValue ? name.Value : "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = identifier,
            CreatedAt = createdAt.HasValue ? createdAt.Value : TimeProvider.GetUtcNow(),
            ModifiedAt = modifiedAt.HasValue ? modifiedAt.Value : TimeProvider.GetUtcNow(),
            UnitStatus = unitStatus.HasValue ? unitStatus.Value : "N",
            UnitType = unitType.HasValue ? unitType.Value : "AS",
            TelephoneNumber = telephoneNumber.HasValue ? telephoneNumber.Value : null,
            MobileNumber = mobileNumber.HasValue ? mobileNumber.Value : null,
            FaxNumber = faxNumber.HasValue ? faxNumber.Value : null,
            EmailAddress = emailAddress.HasValue ? emailAddress.Value : null,
            InternetAddress = internetAddress.HasValue ? internetAddress.Value : null,
            MailingAddress = mailingAddress.HasValue ? mailingAddress.Value : null,
            BusinessAddress = businessAddress.HasValue ? businessAddress.Value : null,
        });

        Assert.True(result.IsSuccess);
        return (OrganizationRecord)result.Value;
    }

    private async Task AddRole(
        PartySource roleSource,
        string roleIdentifier,
        Guid from,
        Guid to)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            INSERT INTO register.external_role (source, identifier, from_party, to_party)
            VALUES (@source, @identifier, @from, @to)
            """;

        cmd.Parameters.Add<PartySource>("source").TypedValue = roleSource;
        cmd.Parameters.Add<string>("identifier", NpgsqlDbType.Text).TypedValue = roleIdentifier;
        cmd.Parameters.Add<Guid>("from", NpgsqlDbType.Uuid).TypedValue = from;
        cmd.Parameters.Add<Guid>("to", NpgsqlDbType.Uuid).TypedValue = to;

        await cmd.PrepareAsync();
        await cmd.ExecuteNonQueryAsync();
    }
}
