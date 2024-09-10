using Altinn.Platform.Register.Models;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Altinn.Register.Persistence.Tests;

public class PostgreSqlPartyPersistenceTests
    : DatabaseTestBase
{
    private readonly static Guid OrganizationWithChildrenUuid = Guid.Parse("b6368d0a-bce4-4798-8460-f4f86fc354c2");
    private readonly static Guid ChildOrganizationUuid = Guid.Parse("08cb91ff-75a4-45a4-b141-3c6be1bf8728");
    private readonly static Guid PersonUuid = Guid.Parse("1dee49bc-7758-43d9-b5de-63c79dbe13a3");

    private IUnitOfWork? _unitOfWork;
    private NpgsqlConnection? _connection;
    private PostgreSqlPartyPersistence? _persistence;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        _unitOfWork = await uowManager.CreateAsync(activityName: "test");
        _connection = _unitOfWork.GetRequiredService<NpgsqlConnection>();
        _persistence = (PostgreSqlPartyPersistence)_unitOfWork.GetRequiredService<IPartyPersistence>();
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
        party.PartyId.Should().Be(50056131);
        party.PartyType.Should().Be(PartyType.Organization);
        party.Name.Should().Be("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.Should().BeNull();
        party.OrganizationIdentifier.Should().HaveValue().Which.Should().Be("910114166");

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
        party.PartyId.Should().Be(50056131);
        party.PartyType.Should().Be(PartyType.Organization);
        party.Name.Should().Be("MOEN OG BJØRNEVATN");
        party.PersonIdentifier.Should().BeNull();
        party.OrganizationIdentifier.Should().HaveValue().Which.Should().Be("910114166");

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
        party.PersonIdentifier.Should().HaveValue().Which.Should().Be("01056261032");
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
            parent.PartyId.Should().Be(50056131);
            parent.PartyType.Should().Be(PartyType.Organization);
            parent.Name.Should().Be("MOEN OG BJØRNEVATN");
            parent.PersonIdentifier.Should().BeNull();
            parent.OrganizationIdentifier.Should().HaveValue().Which.Should().Be("910114166");

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
}
