#nullable enable

using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.MassTransit;
using Xunit.Abstractions;

namespace Altinn.Register.Tests.PartyImport;

public class PartyImportFlowTests(ITestOutputHelper output)
    : BusTestBase(output)
{
    protected override ITestOutputHelper? TestOutputHelper => output;

    private IUnitOfWorkManager UOW => GetRequiredService<IUnitOfWorkManager>();

    [Fact]
    public async Task UpsertPartyCommand_UpsertsParty()
    {
        var partyUuid = Guid.NewGuid();
        var partyId = 1;

        var person = new PersonRecord
        {
            PartyUuid = partyUuid,
            PartyId = partyId,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = new DateOnly(1919, 7, 25),
            DateOfDeath = FieldValue.Null,
        };

        var cmd1 = new UpsertPartyCommand
        {
            Party = person,
        };

        await CommandSender.Send(cmd1);
        var conversation = await Harness.Conversation(cmd1);
        var updateEvent = await conversation.Events.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync();
        
        Assert.NotNull(updateEvent);
        Assert.Equal(partyUuid, updateEvent.Party.PartyUuid);

        {
            await using var uow = await UOW.CreateAsync();
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(partyUuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).FirstAsync();

            fromDb.Should().BeEquivalentTo(person with { VersionId = fromDb!.VersionId });
        }

        var personUpdated = person with
        {
            Address = new StreetAddress
            {
                StreetName = "Testveien",
                HouseNumber = "1",
                PostalCode = "1234",
                City = "Testby",
            },
            DateOfDeath = new DateOnly(2018, 4, 12),
        };

        var cmd2 = new UpsertPartyCommand
        {
            Party = personUpdated,
        };

        await CommandSender.Send(cmd2);
        conversation = await Harness.Conversation(cmd2);
        updateEvent = await conversation.Events.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync();

        Assert.NotNull(updateEvent);
        Assert.Equal(partyUuid, updateEvent.Party.PartyUuid);

        {
            await using var uow = await UOW.CreateAsync();
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(partyUuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).FirstAsync();
            fromDb.Should().BeEquivalentTo(personUpdated with { VersionId = fromDb!.VersionId });
        }
    }

    [Fact]
    public async Task Batches_With_Errors()
    {
        var person1 = new PersonRecord
        {
            PartyUuid = Guid.NewGuid(),
            PartyId = 1,
            DisplayName = "Test Mid Testson",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
            ShortName = "TESTSON Test Mid",
            Address = null,
            MailingAddress = null,
            DateOfBirth = new DateOnly(1919, 7, 25),
            DateOfDeath = FieldValue.Null,
        };

        var person2 = person1 with { PartyUuid = Guid.NewGuid(), PartyId = 2 };

        IReadOnlyList<UpsertPartyCommand> cmds = [
            new UpsertPartyCommand { Party = person1 },
            new UpsertPartyCommand { Party = person2 },
        ];

        await CommandSender.Send(cmds);

        var evts = Harness.Published.SelectAsync<PartyUpdatedEvent>();
        var updateEvent = await evts.FirstOrDefaultAsync();
        Assert.NotNull(updateEvent);

        {
            var insertedPerson = updateEvent.Context.Message.Party.PartyUuid == person1.PartyUuid ? person1 : person2;

            await using var uow = await UOW.CreateAsync();
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(insertedPerson.PartyUuid.Value, PartyFieldIncludes.Party | PartyFieldIncludes.Person).FirstAsync();
            fromDb.Should().BeEquivalentTo(insertedPerson with { VersionId = fromDb!.VersionId });
        }
    }
}
