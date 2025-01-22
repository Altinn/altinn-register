#nullable enable

using Altinn.Register.Contracts.Events;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Core.Utils;
using Altinn.Register.PartyImport;
using Altinn.Register.TestUtils;

namespace Altinn.Register.Tests.PartyImport;

public class PartyImportFlowTests
    : BusTestBase
{
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
            Name = "Test Mid Testson",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
            OrganizationIdentifier = null,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            VersionId = FieldValue.Unset,

            FirstName = "Test",
            MiddleName = "Mid",
            LastName = "Testson",
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
        var conversationId = await Harness.Consumed.SelectAsync<UpsertPartyCommand>(m => m.Context.CorrelationId == cmd1.CommandId).Select(m => m.Context.ConversationId).FirstAsync();
        var consumed = await Harness.Consumed.SelectAsync<UpsertValidatedPartyCommand>(m => m.Context.ConversationId == conversationId).FirstAsync();

        var published = await Harness.Published.SelectAsync<PartyUpdatedEvent>(m => m.Context.ConversationId == conversationId).FirstAsync();

        published.Context.Message.PartyUuid.Should().Be(partyUuid);

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
        conversationId = await Harness.Consumed.SelectAsync<UpsertPartyCommand>(m => m.Context.CorrelationId == cmd2.CommandId).Select(m => m.Context.ConversationId).FirstAsync();
        consumed = await Harness.Consumed.SelectAsync<UpsertValidatedPartyCommand>(m => m.Context.ConversationId == conversationId).FirstAsync();

        published = await Harness.Published.SelectAsync<PartyUpdatedEvent>(m => m.Context.ConversationId == conversationId).FirstAsync();
        Assert.NotNull(published);

        published.Context.Message.PartyUuid.Should().Be(partyUuid);

        {
            await using var uow = await UOW.CreateAsync();
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(partyUuid, PartyFieldIncludes.Party | PartyFieldIncludes.Person).FirstAsync();
            fromDb.Should().BeEquivalentTo(personUpdated with { VersionId = fromDb!.VersionId });
        }
    }
}
