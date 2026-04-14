using System.Data;
using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Persistence.Tests.Utils;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.TestData;
using Altinn.Urn;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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
        => _unitOfWork!.CommitAsync(CancellationToken);

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
                await uow.CommitAsync(CancellationToken);
            }

            await uow.DisposeAsync();
        }

        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        _unitOfWork = await uowManager.CreateAsync(activityName: "test", cancellationToken: CancellationToken);
        _connection = _unitOfWork.GetRequiredService<NpgsqlConnection>();
        _persistence = _unitOfWork.GetRequiredService<PostgreSqlPartyPersistence>();
    }

    [Fact]
    public void CanGet_IPartyPersistence()
    {
        var persistence = _unitOfWork!.GetPartyPersistence();
        persistence.ShouldBeSameAs(Persistence);
    }

    [Fact]
    public void CanGet_IPartyRolePersistence()
    {
        var persistence = _unitOfWork!.GetPartyExternalRolePersistence();
        persistence.ShouldBeSameAs(Persistence);
    }

    [Fact]
    public async Task GetPartyById_NoneExistingUuid_ReturnsEmpty()
    {
        var partyUuid = Guid.Parse("F0000000-0000-0000-0000-000000000000");
        var result = await Persistence
            .GetPartyById(partyUuid, cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPartyById_NoneExistingId_ReturnsEmpty()
    {
        var partyId = 0U;
        var result = await Persistence
            .GetPartyById(partyId, cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPartyById_Returns_SingleParty()
    {
        var result = await Persistence
            .GetPartyById(OrganizationWithChildrenUuid, cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        var party = result.ShouldHaveSingleItem().ShouldBeOfType<OrganizationRecord>();
        party.ParentOrganizationUuid.ShouldBeUnset();

        party.PartyUuid.ShouldBe(OrganizationWithChildrenUuid);
        party.PartyId.ShouldBe(OrganizationWithChildrenId);
        party.PartyType.ShouldBe(PartyRecordType.Organization);
        party.DisplayName.ShouldBe("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.ShouldBeNull();
        party.OrganizationIdentifier.ShouldBe(OrganizationWithChildrenIdentifier);

        party.UnitStatus.ShouldBeUnset();
        party.UnitType.ShouldBeUnset();
        party.TelephoneNumber.ShouldBeUnset();
        party.MobileNumber.ShouldBeUnset();
        party.FaxNumber.ShouldBeUnset();
        party.EmailAddress.ShouldBeUnset();
        party.InternetAddress.ShouldBeUnset();
        party.MailingAddress.ShouldBeUnset();
        party.BusinessAddress.ShouldBeUnset();
    }

    [Fact]
    public async Task GetPartyById_CanGet_OrganizationData()
    {
        var result = await Persistence
            .GetPartyById(OrganizationWithChildrenUuid, include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        var party = result.ShouldHaveSingleItem().ShouldBeOfType<OrganizationRecord>();
        party.ParentOrganizationUuid.ShouldBeUnset();

        party.PartyUuid.ShouldBe(OrganizationWithChildrenUuid);
        party.PartyId.ShouldBe(OrganizationWithChildrenId);
        party.PartyType.ShouldBe(PartyRecordType.Organization);
        party.DisplayName.ShouldBe("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.ShouldBeNull();
        party.OrganizationIdentifier.ShouldBe(OrganizationWithChildrenIdentifier);

        party.UnitStatus.ShouldBe("N");
        party.UnitType.ShouldBe("FLI");
        party.TelephoneNumber.ShouldBeNull();
        party.MobileNumber.ShouldBeNull();
        party.FaxNumber.ShouldBeNull();
        party.EmailAddress.ShouldBe("test@test.test");
        party.InternetAddress.ShouldBeNull();
        party.MailingAddress.ShouldBeNull();
        party.BusinessAddress.ShouldBeNull();
    }

    [Fact]
    public async Task GetPartyByUserId_Returns_SingleParty()
    {
        var person = await UoW.CreatePerson(cancellationToken: CancellationToken);
        var personUserId = person.User.SelectFieldValue(static u => u.UserId).ShouldHaveValue();

        var result = await Persistence
            .GetPartyByUserId(personUserId, include: PartyFieldIncludes.Party, cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        var party = result.ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();

        party.PartyUuid.ShouldBe(person.PartyUuid);
        party.PartyId.ShouldBe(person.PartyId);
        party.PartyType.ShouldBe(person.PartyType);
        party.DisplayName.ShouldBe(person.DisplayName);
        party.PersonIdentifier.ShouldBe(person.PersonIdentifier);
        party.OrganizationIdentifier.ShouldBe(person.OrganizationIdentifier);
        party.User.ShouldBe(new PartyUserRecord(userId: personUserId, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(personUserId)));
    }

    [Fact]
    public async Task GetPartyByUserId_HistoricalId_Returns_SingleParty_WithMultipleUserIds()
    {
        var person = await UoW.CreatePerson(cancellationToken: CancellationToken);
        var personUserIds = person.User.SelectFieldValue(static u => u.UserIds).ShouldHaveValue();
        personUserIds.Count().ShouldBeGreaterThanOrEqualTo(3);

        var personUserId = personUserIds[0];
        var historicalUserId1 = personUserIds[1];
        var historicalUserId2 = personUserIds[2];

        {
            var result = await Persistence
                .GetPartyByUserId(historicalUserId1, include: PartyFieldIncludes.Party, cancellationToken: CancellationToken)
                .ToListAsync(CancellationToken);

            var party = result.ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();

            party.PartyUuid.ShouldBe(person.PartyUuid);
            party.PartyId.ShouldBe(person.PartyId);
            party.PartyType.ShouldBe(person.PartyType);
            party.DisplayName.ShouldBe(person.DisplayName);
            party.PersonIdentifier.ShouldBe(person.PersonIdentifier);
            party.OrganizationIdentifier.ShouldBe(person.OrganizationIdentifier);
            party.User.ShouldBe(new PartyUserRecord(userId: personUserId, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(personUserId, historicalUserId1)));
        }

        {
            var result = await Persistence.GetPartyByUserId(historicalUserId2, include: PartyFieldIncludes.Party, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

            var party = result.ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();

            party.PartyUuid.ShouldBe(person.PartyUuid);
            party.PartyId.ShouldBe(person.PartyId);
            party.PartyType.ShouldBe(person.PartyType);
            party.DisplayName.ShouldBe(person.DisplayName);
            party.PersonIdentifier.ShouldBe(person.PersonIdentifier);
            party.OrganizationIdentifier.ShouldBe(person.OrganizationIdentifier);
            party.User.ShouldBe(new PartyUserRecord(userId: personUserId, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(personUserId, historicalUserId2)));
        }
    }

    [Fact]
    public async Task GetPartyByOrganizationIdentifier_CanGet_OrganizationData()
    {
        var result = await Persistence.GetOrganizationByIdentifier(OrganizationWithChildrenIdentifier, include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        var party = result.ShouldHaveSingleItem().ShouldBeOfType<OrganizationRecord>();
        party.ParentOrganizationUuid.ShouldBeUnset();

        party.PartyUuid.ShouldBe(OrganizationWithChildrenUuid);
        party.PartyId.ShouldBe(OrganizationWithChildrenId);
        party.PartyType.ShouldBe(PartyRecordType.Organization);
        party.DisplayName.ShouldBe("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.ShouldBeNull();
        party.OrganizationIdentifier.ShouldBe(OrganizationWithChildrenIdentifier);

        party.UnitStatus.ShouldBe("N");
        party.UnitType.ShouldBe("FLI");
        party.TelephoneNumber.ShouldBeNull();
        party.MobileNumber.ShouldBeNull();
        party.FaxNumber.ShouldBeNull();
        party.EmailAddress.ShouldBe("test@test.test");
        party.InternetAddress.ShouldBeNull();
        party.MailingAddress.ShouldBeNull();
        party.BusinessAddress.ShouldBeNull();
    }

    [Fact]
    public async Task GetPartyById_CanGet_PersonData()
    {
        var result = await Persistence.GetPartyById(PersonUuid, include: PartyFieldIncludes.Party | PartyFieldIncludes.Person, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        var party = result.ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();
        party.PartyUuid.ShouldBe(PersonUuid);
        party.PartyId.ShouldBe(50002129);
        party.PartyType.ShouldBe(PartyRecordType.Person);
        party.DisplayName.ShouldBe("SANNE BJØRKUM");
        party.PersonIdentifier.ShouldBe(PersonIdentifier);
        party.OrganizationIdentifier.ShouldBeNull();

        party.FirstName.ShouldBe("SANNE");
        party.MiddleName.ShouldBeNull();
        party.LastName.ShouldBe("BJØRKUM");
        party.Address.ShouldBe(new StreetAddressRecord { PostalCode = "3230", City = "SANDEFJORD" });
        party.MailingAddress.ShouldBe(new MailingAddressRecord { Address = "Granholmveien 19", PostalCode = "3230", City = "SANDEFJORD" });
        party.DateOfBirth.ShouldBe(new DateOnly(1862, 05, 01));
        party.DateOfDeath.ShouldBeNull();
    }

    [Fact]
    public async Task GetPartyByPersonIdentifier_CanGet_PersonData()
    {
        var result = await Persistence.GetPersonByIdentifier(PersonIdentifier, include: PartyFieldIncludes.Party | PartyFieldIncludes.Person, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        var party = result.ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();
        party.PartyUuid.ShouldBe(PersonUuid);
        party.PartyId.ShouldBe(50002129);
        party.PartyType.ShouldBe(PartyRecordType.Person);
        party.DisplayName.ShouldBe("SANNE BJØRKUM");
        party.PersonIdentifier.ShouldBe(PersonIdentifier);
        party.OrganizationIdentifier.ShouldBeNull();

        party.FirstName.ShouldBe("SANNE");
        party.MiddleName.ShouldBeNull();
        party.LastName.ShouldBe("BJØRKUM");
        party.Address.ShouldBe(new StreetAddressRecord { PostalCode = "3230", City = "SANDEFJORD" });
        party.MailingAddress.ShouldBe(new MailingAddressRecord { Address = "Granholmveien 19", PostalCode = "3230", City = "SANDEFJORD" });
        party.DateOfBirth.ShouldBe(new DateOnly(1862, 05, 01));
        party.DateOfDeath.ShouldBeNull();
    }

    [Fact]
    public async Task GetPartyById_CanGet_SubUnits()
    {
        var result = await Persistence.GetPartyById(
            OrganizationWithChildrenUuid,
            include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization | PartyFieldIncludes.SubUnits,
            cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        result.Count.ShouldBe(2);

        var parent = result[0].ShouldBeOfType<OrganizationRecord>();
        var child = result[1].ShouldBeOfType<OrganizationRecord>();

        {
            parent.PartyUuid.ShouldBe(OrganizationWithChildrenUuid);
            parent.PartyId.ShouldBe(OrganizationWithChildrenId);
            parent.PartyType.ShouldBe(PartyRecordType.Organization);
            parent.DisplayName.ShouldBe("MOEN OG BJØRNEVATN");
            parent.PersonIdentifier.ShouldBeNull();
            parent.OrganizationIdentifier.ShouldBe(OrganizationWithChildrenIdentifier);

            parent.UnitStatus.ShouldBe("N");
            parent.UnitType.ShouldBe("FLI");
            parent.TelephoneNumber.ShouldBeNull();
            parent.MobileNumber.ShouldBeNull();
            parent.FaxNumber.ShouldBeNull();
            parent.EmailAddress.ShouldBe("test@test.test");
            parent.InternetAddress.ShouldBeNull();
            parent.MailingAddress.ShouldBeNull();
            parent.BusinessAddress.ShouldBeNull();

            parent.ParentOrganizationUuid.ShouldBeUnset();
        }

        {
            child.PartyUuid.ShouldBe(ChildOrganizationUuid);
            child.PartyId.ShouldBe(50056655);
            child.PartyType.ShouldBe(PartyRecordType.Organization);
            child.DisplayName.ShouldBe("NERLANDSØY OG DYRANUT");
            child.PersonIdentifier.ShouldBeNull();
            child.OrganizationIdentifier.ShouldHaveValue().ShouldBe(OrganizationIdentifier.Parse("910056077"));

            child.UnitStatus.ShouldBe("N");
            child.UnitType.ShouldBe("BEDR");
            child.TelephoneNumber.ShouldBeNull();
            child.MobileNumber.ShouldBeNull();
            child.FaxNumber.ShouldBeNull();
            child.EmailAddress.ShouldBe("test@test.test");
            child.InternetAddress.ShouldBeNull();
            child.MailingAddress.ShouldBeNull();
            child.BusinessAddress.ShouldBeNull();

            child.ParentOrganizationUuid.ShouldBe(OrganizationWithChildrenUuid);
        }
    }

    [Fact]
    public async Task LookupParties_MultipleIdentifiers_ToSameParty_ReturnsSingleParty()
    {
        var result = await Persistence.LookupParties(
            partyUuids: [OrganizationWithChildrenUuid],
            partyIds: [OrganizationWithChildrenId],
            organizationIdentifiers: [OrganizationWithChildrenIdentifier],
            cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        var party = result.ShouldHaveSingleItem().ShouldBeOfType<OrganizationRecord>();
        party.ParentOrganizationUuid.ShouldBeUnset();

        party.PartyUuid.ShouldBe(OrganizationWithChildrenUuid);
        party.PartyId.ShouldBe(OrganizationWithChildrenId);
        party.PartyType.ShouldBe(PartyRecordType.Organization);
        party.DisplayName.ShouldBe("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.ShouldBeNull();
        party.OrganizationIdentifier.ShouldBe(OrganizationWithChildrenIdentifier);

        party.UnitStatus.ShouldBeUnset();
        party.UnitType.ShouldBeUnset();
        party.TelephoneNumber.ShouldBeUnset();
        party.MobileNumber.ShouldBeUnset();
        party.FaxNumber.ShouldBeUnset();
        party.EmailAddress.ShouldBeUnset();
        party.InternetAddress.ShouldBeUnset();
        party.MailingAddress.ShouldBeUnset();
        party.BusinessAddress.ShouldBeUnset();
    }

    [Fact]
    public async Task LookupParties_CanReturn_MultipleParties()
    {
        var person1 = await UoW.CreatePerson(cancellationToken: CancellationToken);
        var person2 = await UoW.CreatePerson(cancellationToken: CancellationToken);
        var si = await UoW.CreateSelfIdentifiedUser(type: SelfIdentifiedUserType.IdPortenEmail, cancellationToken: CancellationToken);
        var sysUser = await UoW.CreateSystemUser(OrganizationWithChildrenUuid, cancellationToken: CancellationToken);

        var person1UserId = person1.User.SelectFieldValue(static u => u.UserId).ShouldHaveValue();
        var person2UserId = person2.User.SelectFieldValue(static u => u.UserId).ShouldHaveValue();
        var person2HistoricalUserId = person2.User.SelectFieldValue(static u => u.UserIds).ShouldHaveValue()[1];

        var result = await Persistence.LookupParties(
            organizationIdentifiers: [OrganizationWithChildrenIdentifier],
            personIdentifiers: [PersonIdentifier],
            userIds: [person1UserId, person2HistoricalUserId],
            selfIdentifiedEmails: [si.Email.Value!],
            externalUrns: [sysUser.ExternalUrn.Value!],
            cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        result.Count.ShouldBe(6);

        var testDataPers = result.Where(static p => p.PartyUuid == PersonUuid).ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();
        var testDataOrg = result.Where(static p => p.PartyUuid == OrganizationWithChildrenUuid).ShouldHaveSingleItem().ShouldBeOfType<OrganizationRecord>();
        var person1Result = result.Where(p => p.PartyUuid == person1.PartyUuid).ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();
        var person2Result = result.Where(p => p.PartyUuid == person2.PartyUuid).ShouldHaveSingleItem().ShouldBeOfType<PersonRecord>();
        var siResult = result.Where(p => p.PartyUuid == si.PartyUuid).ShouldHaveSingleItem().ShouldBeOfType<SelfIdentifiedUserRecord>();
        var sysUserResult = result.Where(p => p.PartyUuid == sysUser.PartyUuid).ShouldHaveSingleItem().ShouldBeOfType<SystemUserRecord>();

        person1Result.User.ShouldHaveValue().UserIds.ShouldBe(ImmutableValueArray.Create(person1UserId));
        person2Result.User.ShouldHaveValue().UserIds.ShouldBe(ImmutableValueArray.Create(person2UserId, person2HistoricalUserId));
    }

    [Fact]
    public async Task LookupParties_With_SubUnits_OrdersCorrectly()
    {
        var result = await Persistence.LookupParties(
            partyUuids: [
                Guid.Parse("b6368d0a-bce4-4798-8460-f4f86fc354c2"),
                Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8"),
            ],
            include: PartyFieldIncludes.Party | PartyFieldIncludes.SubUnits,
            cancellationToken: CancellationToken)
            .Cast<OrganizationRecord>()
            .ToListAsync(CancellationToken);

        result.Count.ShouldBe(6);

        result[0].PartyUuid.ShouldHaveValue().ShouldBe(Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8"));
        result[0].ParentOrganizationUuid.ShouldBeUnset();

        result[1].PartyUuid.ShouldHaveValue().ShouldBe(Guid.Parse("4b28742a-5cd0-400e-a096-bd9817d12dca"));
        result[1].ParentOrganizationUuid.ShouldHaveValue().ShouldBe(Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8"));

        result[2].PartyUuid.ShouldHaveValue().ShouldBe(Guid.Parse("ad18578d-94cb-4774-8f37-5b24801c219b"));
        result[2].ParentOrganizationUuid.ShouldHaveValue().ShouldBe(Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8"));

        result[3].PartyUuid.ShouldHaveValue().ShouldBe(Guid.Parse("ec09feda-5dba-4b84-ad0b-f7886e6082cd"));
        result[3].ParentOrganizationUuid.ShouldHaveValue().ShouldBe(Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8"));

        result[4].PartyUuid.ShouldHaveValue().ShouldBe(Guid.Parse("b6368d0a-bce4-4798-8460-f4f86fc354c2"));
        result[4].ParentOrganizationUuid.ShouldBeUnset();

        result[5].PartyUuid.ShouldHaveValue().ShouldBe(Guid.Parse("08cb91ff-75a4-45a4-b141-3c6be1bf8728"));
        result[5].ParentOrganizationUuid.ShouldHaveValue().ShouldBe(Guid.Parse("b6368d0a-bce4-4798-8460-f4f86fc354c2"));
    }

    [Fact]
    public async Task LookupParties_Shared_SubUnit()
    {
        var child = await UoW.CreateOrg(unitType: "BEDR", cancellationToken: CancellationToken);
        var parent1 = await UoW.CreateOrg(unitType: "AS", cancellationToken: CancellationToken);
        var parent2 = await UoW.CreateOrg(unitType: "AS", cancellationToken: CancellationToken);

        await UoW.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: child.PartyUuid.Value, to: parent1.PartyUuid.Value, cancellationToken: CancellationToken);
        await UoW.AddRole(ExternalRoleSource.CentralCoordinatingRegister, "hovedenhet", from: child.PartyUuid.Value, to: parent2.PartyUuid.Value, cancellationToken: CancellationToken);

        var result = await Persistence.LookupParties(
            partyUuids: [parent1.PartyUuid.Value, parent2.PartyUuid.Value],
            include: PartyFieldIncludes.Party | PartyFieldIncludes.Organization | PartyFieldIncludes.SubUnits,
            cancellationToken: CancellationToken)
            .Cast<OrganizationRecord>()
            .ToListAsync(CancellationToken);

        result.Count.ShouldBe(4);

        List<OrganizationRecord> parents = [result[0], result[2]];
        parents.Sort(static (a, b) => a.VersionId.Value.CompareTo(b.VersionId.Value));

        result[0].PartyUuid.ShouldBe(parents[0].PartyUuid);
        result[0].ParentOrganizationUuid.ShouldBeUnset();

        result[1].PartyUuid.ShouldBe(child.PartyUuid);
        result[1].ParentOrganizationUuid.ShouldBe(parents[0].PartyUuid);

        result[2].PartyUuid.ShouldBe(parents[1].PartyUuid);
        result[2].ParentOrganizationUuid.ShouldBeUnset();

        result[3].PartyUuid.ShouldBe(child.PartyUuid);
        result[3].ParentOrganizationUuid.ShouldBe(parents[1].PartyUuid);
    }

    [Fact]
    public async Task LookupParties_ByUsername_MinimalReturn()
    {
        var userId = (await UoW.GetNewUserIds(count: 1, cancellationToken: CancellationToken))[0];
        var person = await UoW.CreatePerson(user: new PartyUserRecord(userId, "some-user-name"), cancellationToken: CancellationToken);

        var result = await Persistence.LookupParties(
            usernames: ["some-user-name"],
            include: PartyFieldIncludes.Username,
            cancellationToken: CancellationToken)
            .Cast<PersonRecord>()
            .ToListAsync(CancellationToken);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetRolesFromNonExistingParty_ReturnsEmpty()
    {
        var partyUuid = Guid.Empty;

        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(partyUuid, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRolesToNonExistingParty_ReturnsEmpty()
    {
        var partyUuid = Guid.Empty;

        var roles = await Persistence.GetExternalRoleAssignmentsToParty(partyUuid, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRolesFromParty_ReturnsRoles()
    {
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(ChildOrganizationUuid, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        var role = roles.ShouldHaveSingleItem();
        role.Source.ShouldBe(ExternalRoleSource.CentralCoordinatingRegister);
        role.Identifier.ShouldBe("hovedenhet");
        role.FromParty.ShouldBe(ChildOrganizationUuid);
        role.ToParty.ShouldBe(OrganizationWithChildrenUuid);

        role.Name.ShouldBeUnset();
        role.Description.ShouldBeUnset();
    }

    [Fact]
    public async Task GetRoles_CanInclude_RoleDefinitions()
    {
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(ChildOrganizationUuid, include: PartyExternalRoleAssignmentFieldIncludes.RoleAssignment | PartyExternalRoleAssignmentFieldIncludes.RoleDefinition, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        var role = roles.ShouldHaveSingleItem();
        role.Source.ShouldBe(ExternalRoleSource.CentralCoordinatingRegister);
        role.Identifier.ShouldBe("hovedenhet");
        role.FromParty.ShouldBe(ChildOrganizationUuid);
        role.ToParty.ShouldBe(OrganizationWithChildrenUuid);

        IReadOnlyDictionary<LangCode, string> name = role.Name.ShouldHaveValue();
        IReadOnlyDictionary<LangCode, string> description = role.Description.ShouldHaveValue();

        name[LangCode.En].ShouldBe("Has as the registration entity");
        name[LangCode.Nb].ShouldBe("Har som registreringsenhet");
        name[LangCode.Nn].ShouldBe("Har som registreringseininga");

        description[LangCode.En].ShouldBe("Has as the registration entity");
        description[LangCode.Nb].ShouldBe("Har som registreringsenhet");
        description[LangCode.Nn].ShouldBe("Har som registreringseininga");
    }

    [Fact]
    public async Task GetRolesToParty_ReturnsRoles()
    {
        var party = Guid.Parse("e2081abd-a16f-4302-93b0-05aaa42023e8");
        var roles = await Persistence.GetExternalRoleAssignmentsToParty(party, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        roles.Count.ShouldBe(3);

        foreach (var role in roles)
        {
            role.Source.ShouldBe(ExternalRoleSource.CentralCoordinatingRegister);
            role.Identifier.ShouldBe("hovedenhet");
            role.FromParty.ShouldHaveValue();
            role.ToParty.ShouldBe(party);

            role.Name.ShouldBeUnset();
            role.Description.ShouldBeUnset();
        }
    }

    [Fact]
    public async Task GetRolesToParty_FilterByRole()
    {
        var role1 = await UoW.CreateFakeRoleDefinition(ExternalRoleSource.CentralCoordinatingRegister, "fake", CancellationToken);
        var role2 = await UoW.CreateFakeRoleDefinition(ExternalRoleSource.CentralCoordinatingRegister, "fake-other", CancellationToken);

        var org1 = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var org2 = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var org3 = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var org4 = await UoW.CreateOrg(cancellationToken: CancellationToken);

        await Persistence.UpsertExternalRolesFromPartyBySource(
            commandId: Guid.CreateVersion7(),
            partyUuid: org2.PartyUuid.Value,
            roleSource: ExternalRoleSource.CentralCoordinatingRegister,
            assignments: [
                new(role1.Identifier, org1.PartyUuid.Value),
                new(role2.Identifier, org1.PartyUuid.Value),
            ],
            cancellationToken: CancellationToken);

        await Persistence.UpsertExternalRolesFromPartyBySource(
            commandId: Guid.CreateVersion7(),
            partyUuid: org3.PartyUuid.Value,
            roleSource: ExternalRoleSource.CentralCoordinatingRegister,
            assignments: [
                new(role1.Identifier, org1.PartyUuid.Value),
            ],
            cancellationToken: CancellationToken);

        await Persistence.UpsertExternalRolesFromPartyBySource(
            commandId: Guid.CreateVersion7(),
            partyUuid: org4.PartyUuid.Value,
            roleSource: ExternalRoleSource.CentralCoordinatingRegister,
            assignments: [
                new(role2.Identifier, org1.PartyUuid.Value),
            ],
            cancellationToken: CancellationToken);

        var roles = await Persistence.GetExternalRoleAssignmentsToParty(
            partyUuid: org1.PartyUuid.Value,
            role: new(role1.Source, role1.Identifier),
            cancellationToken: CancellationToken)
            .ToListAsync(CancellationToken);

        roles.Count.ShouldBe(2);
        roles.Where(r => r.FromParty.Value == org2.PartyUuid.Value).ShouldHaveSingleItem();
        roles.Where(r => r.FromParty.Value == org3.PartyUuid.Value).ShouldHaveSingleItem();
        foreach (var roleAssignment in roles)
        {
            roleAssignment.ToParty.ShouldBe(org1.PartyUuid.Value);
        }
    }

    [Theory]
    [InlineData(PartyFieldIncludes.Party)]
    [InlineData(PartyFieldIncludes.Identifiers)]
    [InlineData(PartyFieldIncludes.Party | PartyFieldIncludes.Organization | PartyFieldIncludes.Person)]
    public async Task GetPartyStream(PartyFieldIncludes includes)
    {
        var items = await Persistence.GetPartyStream(0, 100, includes, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

        items.Count.ShouldBe(100);
    }

    #region Upsert Org

    [Fact]
    public async Task UpsertParty_Org_Inserts_New_Org()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var orgNo = await UoW.GetNewOrgNumber(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.OrganizationId.Create(orgNo),
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
            Source = orgNo.ToString().StartsWith('0') ? OrganizationSource.BusinessAssessedPartnerships : OrganizationSource.CentralCoordinatingRegister,
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<OrganizationRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_Updates_Name_And_Updated_And_OrgProps()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var orgNo = await UoW.GetNewOrgNumber(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.OrganizationId.Create(orgNo),
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
            Source = orgNo.ToString().StartsWith('0') ? OrganizationSource.BusinessAssessedPartnerships : OrganizationSource.CentralCoordinatingRegister,
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

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

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<OrganizationRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_Cannot_Update_Id()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var orgNo = await UoW.GetNewOrgNumber(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.OrganizationId.Create(orgNo),
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
            Source = orgNo.ToString().StartsWith('0') ? OrganizationSource.BusinessAssessedPartnerships : OrganizationSource.CentralCoordinatingRegister,
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            PartyId = id + 1,
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldBeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_Cannot_Update_OrgNr()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var orgNo = await UoW.GetNewOrgNumber(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.OrganizationId.Create(orgNo),
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
            Source = orgNo.ToString().StartsWith('0') ? OrganizationSource.BusinessAssessedPartnerships : OrganizationSource.CentralCoordinatingRegister,
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            OrganizationIdentifier = await UoW.GetNewOrgNumber(CancellationToken),
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldBeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Org_CannotInsert_WithSamePartyId()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var orgNo = await UoW.GetNewOrgNumber(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new OrganizationRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.OrganizationId.Create(orgNo),
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
            Source = orgNo.ToString().StartsWith('0') ? OrganizationSource.BusinessAssessedPartnerships : OrganizationSource.CentralCoordinatingRegister,
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var uuid2 = Guid.NewGuid();
        var orgNo2 = await UoW.GetNewOrgNumber(CancellationToken);
        var toInsert2 = new OrganizationRecord
        {
            PartyUuid = uuid2,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.OrganizationId.Create(orgNo2),
            DisplayName = "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = orgNo2,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            Source = orgNo2.ToString().StartsWith('0') ? OrganizationSource.BusinessAssessedPartnerships : OrganizationSource.CentralCoordinatingRegister,
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
        result = await Persistence.UpsertParty(toInsert2, cancellationToken: CancellationToken);
        result.ShouldBeProblem(Problems.PartyConflict.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid2, PartyFieldIncludes.Party | PartyFieldIncludes.Organization, cancellationToken: CancellationToken).ToListAsync(CancellationToken);
        fromDb.ShouldBeEmpty();
    }

    #endregion

    #region Upsert Person

    [Fact]
    public async Task UpsertParty_Person_Inserts_New_Person()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<PersonRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_Updates_Name_And_Updated_And_PersonProps()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

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

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<PersonRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_Cannot_Update_Id()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            PartyId = id + 1,
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldBeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_Cannot_Update_PersonIdentifier()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            PersonIdentifier = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken),
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldBeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_Person_CannotInsert_WithSamePartyId()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var uuid2 = Guid.NewGuid();
        var toInsert2 = new PersonRecord
        {
            PartyUuid = uuid2,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
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
        result = await Persistence.UpsertParty(toInsert2, cancellationToken: CancellationToken);
        result.ShouldBeProblem(Problems.PartyConflict.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid2, PartyFieldIncludes.Party | PartyFieldIncludes.Person, cancellationToken: CancellationToken).ToListAsync(CancellationToken);
        fromDb.ShouldBeEmpty();
    }

    #endregion

    #region Upsert Self-Identified User

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_Inserts_New_SelfIdentifiedUser_NoType()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = FieldValue.Null,
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
            SelfIdentifiedUserType = FieldValue.Null,
            Email = FieldValue.Null,
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<SelfIdentifiedUserRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_Inserts_New_SelfIdentifiedUser_Legacy()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create("test_si_user")),
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
            SelfIdentifiedUserType = SelfIdentifiedUserType.Legacy,
            Email = FieldValue.Null,
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<SelfIdentifiedUserRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_Inserts_New_SelfIdentifiedUser_Edu()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = FieldValue.Null,
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
            SelfIdentifiedUserType = SelfIdentifiedUserType.Educational,
            Email = FieldValue.Null,
            ExtRef = uuid.ToString(),
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<SelfIdentifiedUserRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_Inserts_New_SelfIdentifiedUser_IdPortenEmail()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.IDPortenEmail.Create(UrnEncoded.Create("test@example.com")),
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
            SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
            Email = "test-si-user@example.com",
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<SelfIdentifiedUserRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Theory]
    [EnumMembersData<SelfIdentifiedUserType>]
    public async Task UpsertParty_SelfIdentified_Can_Update_SelfIdentifiedType_FromNull(SelfIdentifiedUserType type)
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = FieldValue.Null,
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
            SelfIdentifiedUserType = FieldValue.Null,
            Email = FieldValue.Null,
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = type switch
        {
            SelfIdentifiedUserType.Legacy => toInsert with
            {
                SelfIdentifiedUserType = type,
                Email = FieldValue.Null,
                ExtRef = FieldValue.Null,
                ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create("test_si_user")),
            },
            SelfIdentifiedUserType.Educational => toInsert with
            {
                SelfIdentifiedUserType = type,
                Email = FieldValue.Null,
                ExtRef = uuid.ToString(),
                ExternalUrn = FieldValue.Null,
            },
            SelfIdentifiedUserType.IdPortenEmail => toInsert with
            {
                SelfIdentifiedUserType = type,
                Email = "test-si-user@example.com",
                ExtRef = FieldValue.Null,
                ExternalUrn = PartyExternalRefUrn.IDPortenEmail.Create(UrnEncoded.Create("test-si-user@example.com")),
            },
            _ => throw new UnreachableException($"Invalid {nameof(SelfIdentifiedUserType)}: {type}"),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<SelfIdentifiedUserRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentified_Cannot_Update_SelfIdentifiedType_FromNonNull()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create("test_si_user")),
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
            SelfIdentifiedUserType = SelfIdentifiedUserType.Legacy,
            Email = FieldValue.Null,
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            SelfIdentifiedUserType = SelfIdentifiedUserType.Educational,
            ExternalUrn = FieldValue.Null,
            ExtRef = uuid.ToString(),
        };

        await NewTransaction();
        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldBeProblem(Problems.InvalidPartyUpdate.ErrorCode);

        await NewTransaction(commit: false);
        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_Updates_Name_And_Updated()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create("test_si_user")),
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
            SelfIdentifiedUserType = SelfIdentifiedUserType.Legacy,
            Email = FieldValue.Null,
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<SelfIdentifiedUserRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpsertParty_SelfIdentifiedUser_Can_Retain_IsDeleted(bool isDeleted)
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create("test_si_user")),
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
            SelfIdentifiedUserType = SelfIdentifiedUserType.Legacy,
            Email = FieldValue.Null,
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            IsDeleted = FieldValue.Unset,
            DeletedAt = FieldValue.Unset,
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            IsDeleted = toInsert.IsDeleted, // IsDeleted should not change
            DeletedAt = toInsert.DeletedAt, // DeletedAt should not change
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<SelfIdentifiedUserRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SelfIdentifiedUser_IsDeleted_DefaultsFalse()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var uuid = Guid.NewGuid();

        var toInsert = new SelfIdentifiedUserRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create("test_si_user")),
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
            SelfIdentifiedUserType = SelfIdentifiedUserType.Legacy,
            Email = FieldValue.Null,
            ExtRef = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toInsert with
        {
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
        };

        var updated = result.Value.ShouldBeOfType<SelfIdentifiedUserRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SelfIdentifiedUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    #endregion

    #region Upsert System User

    [Theory]
    [InlineData(SystemUserRecordType.Standard)]
    [InlineData(SystemUserRecordType.Agent)]
    public async Task UpsertParty_SystemUser_Inserts_New_SystemUser(SystemUserRecordType type)
    {
        var org = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            ExternalUrn = PartyExternalRefUrn.SystemUserUuid.Create(uuid),
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<SystemUserRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SystemUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SystemUser_Updates_Name_And_Updated()
    {
        var org = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            ExternalUrn = PartyExternalRefUrn.SystemUserUuid.Create(uuid),
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<SystemUserRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SystemUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SystemUser_CanKeepOwner()
    {
        var org = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            ExternalUrn = PartyExternalRefUrn.SystemUserUuid.Create(uuid),
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            OwnerUuid = FieldValue.Unset,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            OwnerUuid = toInsert.OwnerUuid, // owner should not change
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<SystemUserRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party | PartyFieldIncludes.SystemUser, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_SystemUser_Cannot_Update_Owner()
    {
        var org1 = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var org2 = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new SystemUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            ExternalUrn = PartyExternalRefUrn.SystemUserUuid.Create(uuid),
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            OwnerUuid = org2.PartyUuid,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var error = result.ShouldBeProblem(Problems.InvalidPartyUpdate.ErrorCode);
        error.Extensions["column"].Single().ShouldBe("owner");
    }

    #endregion

    #region Upsert Enterprise User

    [Fact]
    public async Task UpsertParty_EnterpriseUser_Inserts_New_EnterpriseUser()
    {
        var org = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new EnterpriseUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            ExternalUrn = FieldValue.Null,
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var inserted = result.ShouldHaveValue().ShouldBeOfType<EnterpriseUserRecord>();
        inserted.ShouldBeEquivalentTo(toInsert with { VersionId = inserted.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(toInsert with { VersionId = fromDb.VersionId });
    }

    [Fact]
    public async Task UpsertParty_EnterpriseUser_Updates_Name_And_Updated()
    {
        var org = await UoW.CreateOrg(cancellationToken: CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new EnterpriseUserRecord
        {
            PartyUuid = uuid,
            PartyId = FieldValue.Null,
            ExternalUrn = FieldValue.Null,
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

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        TimeProvider.Advance(TimeSpan.FromDays(30));

        var toUpdate = toInsert with
        {
            DisplayName = "Test Updated",
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        var expected = toUpdate with
        {
            CreatedAt = toInsert.CreatedAt, // created at should not change
        };

        var updated = result.Value.ShouldBeOfType<EnterpriseUserRecord>();
        updated.ShouldBeEquivalentTo(expected with { VersionId = updated.VersionId });

        var fromDb = await Persistence.GetPartyById(uuid, PartyFieldIncludes.Party, cancellationToken: CancellationToken).SingleAsync(CancellationToken);
        fromDb.ShouldBeEquivalentTo(expected with { VersionId = fromDb.VersionId });
    }

    #endregion

    #region Upsert Role-Assigments

    [Fact]
    public async Task UpsertExternalRolesFromPartyBySource()
    {
        var added = ExternalRoleAssignmentEvent.EventType.Added;
        var removed = ExternalRoleAssignmentEvent.EventType.Removed;
        var source = ExternalRoleSource.CentralCoordinatingRegister;

        await UoW.CreateFakeRoleDefinitions(CancellationToken);

        var party1 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000001"), cancellationToken: CancellationToken)).PartyUuid.Value;
        var party2 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000002"), cancellationToken: CancellationToken)).PartyUuid.Value;
        var party3 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000003"), cancellationToken: CancellationToken)).PartyUuid.Value;
        var party4 = (await UoW.CreateOrg(uuid: Guid.Parse("00000000-0000-0000-0000-000000000004"), cancellationToken: CancellationToken)).PartyUuid.Value;

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
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(party1, cancellationToken: CancellationToken).ToListAsync(CancellationToken);
        roles.Count.ShouldBe(8);

        roles.Where(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-03", party2)).ShouldHaveSingleItem();
        roles.Where(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-04", party2)).ShouldHaveSingleItem();
        roles.Where(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-05", party2)).ShouldHaveSingleItem();
        roles.Where(Matches(ExternalRoleSource.NationalPopulationRegister, "fake-06", party2)).ShouldHaveSingleItem();

        roles.Where(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-01", party3)).ShouldHaveSingleItem();
        roles.Where(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-01", party4)).ShouldHaveSingleItem();
        roles.Where(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-02", party3)).ShouldHaveSingleItem();
        roles.Where(Matches(ExternalRoleSource.CentralCoordinatingRegister, "fake-02", party4)).ShouldHaveSingleItem();

        static Func<PartyExternalRoleAssignmentRecord, bool> Matches(ExternalRoleSource source, string identifier, Guid toParty)
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
                assignments,
                cancellationToken: CancellationToken)
            .Select(static e => new CheckUpsertExternalRolesFromPartyBySourceExpectedEvent(e.Type, e.RoleIdentifier, e.ToParty))
            .ToListAsync(CancellationToken);

        firstTryEvents.Count.ShouldBe(expectedEvents.Count);
        foreach (var expected in expectedEvents)
        {
            firstTryEvents.ShouldContain(expected);
        }

        // idempotency check
        var secondTryEvents = await Persistence
            .UpsertExternalRolesFromPartyBySource(
                cmdId,
                fromParty,
                source,
                assignments,
                cancellationToken: CancellationToken)
            .Select(static e => new CheckUpsertExternalRolesFromPartyBySourceExpectedEvent(e.Type, e.RoleIdentifier, e.ToParty))
            .ToListAsync(CancellationToken);

        secondTryEvents.Count.ShouldBe(expectedEvents.Count);
        foreach (var expected in expectedEvents)
        {
            secondTryEvents.ShouldContain(expected);
        }

        // check that the roles are actually assigned
        var roles = await Persistence.GetExternalRoleAssignmentsFromParty(fromParty, cancellationToken: CancellationToken).Where(r => r.Source == source).ToListAsync(CancellationToken);
        roles.Count.ShouldBe(assignments.Count);
        foreach (var assignment in assignments)
        {
            roles.Where(r => r.Identifier == assignment.RoleIdentifier && r.ToParty == assignment.ToParty).ShouldHaveSingleItem();
        }
    }

    private record CheckUpsertExternalRolesFromPartyBySourceExpectedEvent(ExternalRoleAssignmentEvent.EventType Type, string Identifier, Guid ToParty);

    #endregion

    #region Sequence Transaction handling

    [Fact]
    public async Task Sequence_Transaction_Handling()
    {
        var dataSource = GetRequiredService<NpgsqlDataSource>();
        await using var noTxConn = await dataSource.OpenConnectionAsync(CancellationToken);
        await using var maxSafeCmd = noTxConn.CreateCommand();
        maxSafeCmd.CommandText = /*strpsql*/"SELECT register.tx_max_safeval('register.party_version_id_seq')";
        await maxSafeCmd.PrepareAsync(CancellationToken);

        await using var uow1 = await GetRequiredService<IUnitOfWorkManager>().CreateAsync(activityName: "uow1", cancellationToken: CancellationToken);
        await using var uow2 = await GetRequiredService<IUnitOfWorkManager>().CreateAsync(activityName: "uow2", cancellationToken: CancellationToken);

        var tx1Conn = uow1.GetRequiredService<NpgsqlConnection>();
        var tx2Conn = uow2.GetRequiredService<NpgsqlConnection>();

        Assert.Equal(9223372036854775807UL, await GetVisible());

        var val1 = await NextVal(tx1Conn);
        var val2 = await NextVal(tx2Conn);
        val1.ShouldBeLessThan(val2);

        (await GetVisible()).ShouldBe(val1 - 1);

        await uow2.CommitAsync(CancellationToken);
        (await GetVisible()).ShouldBe(val1 - 1);

        await uow1.CommitAsync(CancellationToken);
        Assert.Equal(9223372036854775807UL, await GetVisible());

        async Task<ulong> GetVisible()
        {
            await using var reader = await maxSafeCmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, CancellationToken);
            Assert.True(await reader.ReadAsync(CancellationToken));

            var result = await reader.GetFieldValueAsync<long>(0, CancellationToken);
            return (ulong)result;
        }

        async Task<ulong> NextVal(NpgsqlConnection conn)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = /*strpsql*/"SELECT register.tx_nextval('register.party_version_id_seq')";
            await cmd.PrepareAsync(CancellationToken);

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, CancellationToken);
            Assert.True(await reader.ReadAsync(CancellationToken));

            var result = await reader.GetFieldValueAsync<long>(0, CancellationToken);
            return (ulong)result;
        }
    }

    #endregion

    #region UserId handling

    [Fact]
    public async Task Person_CanBeUpdated_WithActiveUserId()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue().User.ShouldBeUnset();

        await NewTransaction();
        var userIds = await TestDataGenerator.GetNextUserIds(cancellationToken: CancellationToken);
        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: userIds[0], username: FieldValue.Unset, userIds: userIds.ToImmutableValueArray()),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var updatedUser = result.ShouldHaveValue().User.ShouldHaveValue();
        updatedUser.UserIds.ShouldHaveValue().Single().ShouldBe(userIds[0]);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithActiveUserId_And_Username()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue().User.ShouldBeUnset();

        await NewTransaction();
        var userIds = await TestDataGenerator.GetNextUserIds(cancellationToken: CancellationToken);
        var username = $"user_{userIds[0]}";
        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: userIds[0], username: username, userIds: userIds.ToImmutableValueArray()),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var user = result.ShouldHaveValue().User.ShouldHaveValue();

        user.UserId.ShouldBe(userIds[0]);
        user.UserIds.ShouldHaveValue().Single().ShouldBe(userIds[0]);
        user.Username.ShouldBe(username);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithHistoricalUserIds()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue().User.ShouldBeUnset();

        await NewTransaction();
        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var userIds = result.ShouldHaveValue().User.ShouldHaveValue().UserIds.ShouldHaveValue();

        userIds.Count().ShouldBe(3);
        userIds[0].ShouldBe(10U);
        userIds[1].ShouldBe(5U);
        userIds[2].ShouldBe(2U);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithHistoricalUserIds_Only()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue().User.ShouldBeUnset();

        await NewTransaction();
        var user = new PartyUserRecord(userId: 10U, username: FieldValue.Null, userIds: ImmutableValueArray.Create(10U, 2U, 5U));

        var userResult = await Persistence.UpsertPartyUser(uuid, user, cancellationToken: CancellationToken);
        var userIds = userResult.ShouldHaveValue().UserIds.ShouldHaveValue();

        userIds.Count().ShouldBe(3);
        userIds[0].ShouldBe(10U);
        userIds[1].ShouldBe(5U);
        userIds[2].ShouldBe(2U);
    }

    [Fact]
    public async Task Person_CanBeInserted_WithHistoricalUserIds()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        var userIds = result.ShouldHaveValue().User.ShouldHaveValue().UserIds.ShouldHaveValue();

        userIds.Count().ShouldBe(3);
        userIds[0].ShouldBe(10U);
        userIds[1].ShouldBe(5U);
        userIds[2].ShouldBe(2U);
    }

    [Fact]
    public async Task Person_CanBeUpdated_WithoutTouchingUserIds()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        await NewTransaction();
        var toUpdate = toInsert with
        {
            User = FieldValue.Unset,
            DisplayName = "updated"
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var person = result.ShouldHaveValue().ShouldBeOfType<PersonRecord>();

        person.User.ShouldBeUnset();

        await NewTransaction();
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT user_id, is_active FROM register.user WHERE uuid = @partyId ORDER BY user_id DESC";

        cmd.Parameters.Add<Guid>("partyId", NpgsqlTypes.NpgsqlDbType.Uuid).TypedValue = uuid;

        await using var reader = await cmd.ExecuteReaderAsync(CancellationToken);

        (await reader.ReadAsync(CancellationToken)).ShouldBeTrue();
        reader.GetInt64("user_id").ShouldBe(10);
        reader.GetBoolean("is_active").ShouldBeTrue();

        (await reader.ReadAsync(CancellationToken)).ShouldBeTrue();
        reader.GetInt64("user_id").ShouldBe(5);
        reader.GetBoolean("is_active").ShouldBeFalse();

        (await reader.ReadAsync(CancellationToken)).ShouldBeTrue();
        reader.GetInt64("user_id").ShouldBe(2);
        reader.GetBoolean("is_active").ShouldBeFalse();

        (await reader.ReadAsync(CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Person_Cannot_UpdateActiveUserId()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 12U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(12U, 10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var problem = result.ShouldBeProblem();
        problem.ErrorCode.ShouldBe(Problems.InvalidPartyUpdate.ErrorCode);
        problem.Extensions["column"].Single().ShouldBe("user_id");
    }

    [Fact]
    public async Task Person_Can_UpdateActiveUsername()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: "user2", userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var user = result.ShouldHaveValue().User.ShouldHaveValue();
        user.UserId.ShouldBe(10U);
        user.Username.ShouldBe("user2");
    }

    [Fact]
    public async Task Person_Can_UnsetActiveUsername()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Null, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var user = result.ShouldHaveValue().User.ShouldHaveValue();
        user.UserId.ShouldBe(10U);
        user.Username.ShouldBeNull();
    }

    [Fact]
    public async Task Person_Can_RetainActiveUsername()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var toInsert = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = birthDate,
            DateOfDeath = FieldValue.Null,
        };

        var result = await Persistence.UpsertParty(toInsert, cancellationToken: CancellationToken);
        result.ShouldHaveValue();

        await NewTransaction();

        var toUpdate = toInsert with
        {
            User = new PartyUserRecord(userId: 10U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(10U, 2U, 5U)),
        };

        result = await Persistence.UpsertParty(toUpdate, cancellationToken: CancellationToken);
        var user = result.ShouldHaveValue().User.ShouldHaveValue();
        user.UserId.ShouldBe(10U);
        user.Username.ShouldBe("user1");
    }

    [Fact]
    public async Task UpsertUserRecord_CanCreate_New_UserRecord()
    {
        var person = await UoW.CreatePerson(user: FieldValue.Null, cancellationToken: CancellationToken);

        var userIds = await TestDataGenerator.GetNextUserIds(cancellationToken: CancellationToken);
        await Persistence.UpsertUserRecord(person.PartyUuid.Value, userIds[0], "test-user", isActive: true, cancellationToken: CancellationToken);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User, cancellationToken: CancellationToken).FirstOrDefaultAsync(CancellationToken);
        updated.ShouldNotBeNull();
        updated.User.ShouldHaveValue();
        updated.User.Value!.UserId.ShouldBe(userIds[0]);
        updated.User.Value!.Username.ShouldBe("test-user");
    }

    [Fact]
    public async Task UpsertUserRecord_CanUpdate_ExistingRecord()
    {
        var ids = await TestDataGenerator.GetNextUserIds(cancellationToken: CancellationToken);
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[0], "test-user-name", ImmutableValueArray.ToImmutableValueArray(ids)), cancellationToken: CancellationToken);

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[0], "updated-user-name", isActive: true, cancellationToken: CancellationToken);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User, cancellationToken: CancellationToken).FirstOrDefaultAsync(CancellationToken);
        updated.ShouldNotBeNull();
        updated.User.ShouldHaveValue();
        updated.User.Value!.UserId.ShouldBe(ids[0]);
        updated.User.Value!.Username.ShouldBe("updated-user-name");
    }

    [Fact]
    public async Task UpsertUserRecord_CanDeactivate_User()
    {
        var ids = await TestDataGenerator.GetNextUserIds(cancellationToken: CancellationToken);
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[0], "test-user-name", ImmutableValueArray.ToImmutableValueArray(ids)), cancellationToken: CancellationToken);

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[0], FieldValue.Unset, isActive: false, cancellationToken: CancellationToken);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User, cancellationToken: CancellationToken).FirstOrDefaultAsync(CancellationToken);
        updated.ShouldNotBeNull();
        updated.User.ShouldBeNull();
        updated.VersionId.ShouldHaveValue().ShouldBeGreaterThan(person.VersionId.Value);
    }

    [Fact]
    public async Task UpsertUserRecord_Inactive_DoesNotUpdatePartyVersionId()
    {
        var ids = await TestDataGenerator.GetNextUserIds(count: 2, cancellationToken: CancellationToken);
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[1], "test-user-name", ImmutableValueArray.Create(ids[1])), cancellationToken: CancellationToken);

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[0], "old-user-name", isActive: false, cancellationToken: CancellationToken);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User, cancellationToken: CancellationToken).FirstOrDefaultAsync(CancellationToken);
        updated.ShouldNotBeNull();
        updated.VersionId.ShouldHaveValue().ShouldBe(person.VersionId.Value);
    }

    [Fact]
    public async Task UpsertUserRecord_Active_UpdatesPartyVersionId()
    {
        var ids = await TestDataGenerator.GetNextUserIds(count: 2, cancellationToken: CancellationToken);
        var person = await UoW.CreatePerson(user: new PartyUserRecord(ids[1], "test-user-name", ImmutableValueArray.Create(ids[1])), cancellationToken: CancellationToken);

        await Persistence.UpsertUserRecord(person.PartyUuid.Value, ids[1], "old-user-name", isActive: true, cancellationToken: CancellationToken);

        var updated = await Persistence.GetPartyById(person.PartyUuid.Value, include: PartyFieldIncludes.Party | PartyFieldIncludes.User, cancellationToken: CancellationToken).FirstOrDefaultAsync(CancellationToken);
        updated.ShouldNotBeNull();
        updated.VersionId.ShouldHaveValue().ShouldBeGreaterThan(person.VersionId.Value);
    }

    #endregion

    #region AsyncSingleton

    [Fact]
    public async Task AsyncSingleton_YieldSingleParty()
    {
        var id = await UoW.GetNextPartyId(CancellationToken);
        var birthDate = UoW.GetRandomBirthDate();
        var isDNumber = TestDataGenerator.GetRandomBool(0.1); // 10% chance of D-number
        var personId = await UoW.GetNewPersonIdentifier(birthDate, isDNumber, CancellationToken);
        var uuid = Guid.NewGuid();

        var party = new PersonRecord
        {
            PartyUuid = uuid,
            PartyId = id,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personId),
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
            Source = PersonSource.NationalPopulationRegister,
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
        var list = await singleton.ToListAsync(CancellationToken);

        list.ShouldHaveSingleItem().ShouldBeSameAs(party);
    }

    #endregion

}
