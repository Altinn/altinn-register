using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.TestUtils.MassTransit;

namespace Altinn.Register.IntegrationTests.PartyImport;

public class PartyImportFlowTests
    : IntegrationTestBase
{
    [Fact]
    public async Task UpsertPartyCommand_UpsertsParty()
    {
        await Setup(async (uow, ct) =>
        {
            await GetRequiredService<IImportJobTracker>()
                .TrackQueueStatus("test", new() { EnqueuedMax = 100, SourceMax = null }, ct);
        });

        var partyUuid = Guid.CreateVersion7();
        var partyId = 1U;

        var person = new PersonRecord
        {
            PartyUuid = partyUuid,
            PartyId = partyId,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(PersonIdentifier.Parse("25871999336")),
            DisplayName = "Test Mid Testson",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
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
            DateOfBirth = new DateOnly(1919, 7, 25),
            DateOfDeath = FieldValue.Null,
        };

        var cmd1 = new UpsertPartyCommand
        {
            Party = person,
            Tracking = new("test", 10),
        };

        await CommandSender.Send(cmd1, TestContext.Current.CancellationToken);
        var conversation = await TestHarness.Conversation(cmd1, TestContext.Current.CancellationToken);
        var updateEvent = await conversation.Events.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        updateEvent.ShouldNotBeNull();
        updateEvent.Party.PartyUuid.ShouldBe(partyUuid);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(partyUuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person, ct).FirstAsync(ct);

            fromDb.ShouldBe(person with { VersionId = fromDb!.VersionId });
        });

        var personUpdated = person with
        {
            Address = new StreetAddressRecord
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
            Tracking = new("test", 20),
        };

        await CommandSender.Send(cmd2, TestContext.Current.CancellationToken);
        conversation = await TestHarness.Conversation(cmd2, TestContext.Current.CancellationToken);
        updateEvent = await conversation.Events.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        updateEvent.ShouldNotBeNull();
        updateEvent.Party.PartyUuid.ShouldBe(partyUuid);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(partyUuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person, ct).FirstAsync(ct);
            fromDb.ShouldBe(personUpdated with { VersionId = fromDb!.VersionId });
        });
    }

    [Fact]
    public async Task Batches_With_Errors()
    {
        var person1 = new PersonRecord
        {
            PartyUuid = Guid.NewGuid(),
            PartyId = 1,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(PersonIdentifier.Parse("25871999336")),
            DisplayName = "Test Mid Testson",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
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
            DateOfBirth = new DateOnly(1919, 7, 25),
            DateOfDeath = FieldValue.Null,
        };

        var person2 = person1 with { PartyUuid = Guid.NewGuid(), PartyId = 2 };

        IReadOnlyList<UpsertPartyCommand> cmds = [
            new UpsertPartyCommand { Party = person1 },
            new UpsertPartyCommand { Party = person2 },
        ];

        await CommandSender.Send(cmds, TestContext.Current.CancellationToken);

        var evts = TestHarness.Published.SelectAsync<PartyUpdatedEvent>(TestContext.Current.CancellationToken);
        var updateEvent = await evts.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        updateEvent.ShouldNotBeNull();

        await Check(async (uow, ct) =>
        {
            var insertedPerson = updateEvent.Context.Message.Party.PartyUuid == person1.PartyUuid ? person1 : person2;
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(insertedPerson.PartyUuid.Value, PartyFieldIncludes.Party | PartyFieldIncludes.Person, ct).FirstAsync(ct);
            fromDb.ShouldBe(insertedPerson with { VersionId = fromDb!.VersionId });
        });
    }
}
