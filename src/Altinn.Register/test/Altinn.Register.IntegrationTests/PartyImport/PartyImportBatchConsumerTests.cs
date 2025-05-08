using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.TestUtils.MassTransit;
using Altinn.Register.TestUtils.TestData;

namespace Altinn.Register.IntegrationTests.PartyImport;

public class PartyImportBatchConsumerTests
    : IntegrationTestBase
{
    [Fact]
    public async Task UpsertExternalRoleAssignmentsCommand_UpsertsExternalRoleAssignments()
    {
        await GetRequiredService<IImportJobTracker>()
            .TrackQueueStatus(
                "test", 
                new()
                {
                    SourceMax = 10,
                    EnqueuedMax = 1,
                },
                TestContext.Current.CancellationToken);

        var (org, person1, person2) = await Setup(async (uow, ct) =>
        {
            var org = await uow.CreateOrg(cancellationToken: ct);
            var person1 = await uow.CreatePerson(cancellationToken: ct);
            var person2 = await uow.CreatePerson(cancellationToken: ct);

            var roles = uow.GetPartyExternalRolePersistence();
            await roles.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: org.PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new("styreleder", person2.PartyUuid.Value),
                ],
                cancellationToken: ct);

            return (org, person1, person2);
        });

        var cmd = new UpsertExternalRoleAssignmentsCommand
        {
            FromPartyUuid = org.PartyUuid.Value,
            FromPartyId = org.PartyId.Value,
            Source = ExternalRoleSource.CentralCoordinatingRegister,
            Tracking = new("test", 1),
            Assignments = [
                new() { ToPartyUuid = person1.PartyUuid.Value, Identifier = "daglig-leder" },
                new() { ToPartyUuid = person1.PartyUuid.Value, Identifier = "styremedlem" },
                new() { ToPartyUuid = person2.PartyUuid.Value, Identifier = "styremedlem" },
            ],
        };

        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);
        var consumed = await TestHarness.Consumed.SelectAsync<UpsertExternalRoleAssignmentsCommand>(m => m.Context.CorrelationId == cmd.CommandId, TestContext.Current.CancellationToken).FirstAsync(TestContext.Current.CancellationToken);
        var conversation = TestHarness.Conversation(consumed.Context.ConversationId!.Value, TestContext.Current.CancellationToken);

        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "daglig-leder"), TestContext.Current.CancellationToken));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem"), TestContext.Current.CancellationToken));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem"), TestContext.Current.CancellationToken));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentRemovedEvent>().AnyAsync(MatchRemovedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styreleder"), TestContext.Current.CancellationToken));

        // idempotency check
        await CommandSender.Send(cmd, TestContext.Current.CancellationToken);
        var consumed2 = await TestHarness.Consumed
            .SelectAsync<UpsertExternalRoleAssignmentsCommand>(m => m.Context.CorrelationId == cmd.CommandId && m.Context.ConversationId != consumed.Context.ConversationId, TestContext.Current.CancellationToken)
            .FirstAsync(TestContext.Current.CancellationToken);

        conversation = TestHarness.Conversation(consumed2.Context.ConversationId!.Value, TestContext.Current.CancellationToken);

        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "daglig-leder"), TestContext.Current.CancellationToken));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem"), TestContext.Current.CancellationToken));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem"), TestContext.Current.CancellationToken));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentRemovedEvent>().AnyAsync(MatchRemovedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styreleder"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertPartyUser_Upsers_PartyUser()
    {
        var party = await Setup(async (uow, ct) =>
        {
            await GetRequiredService<IImportJobTracker>().TrackQueueStatus("test", new ImportJobQueueStatus { EnqueuedMax = 1, SourceMax = null }, TestContext.Current.CancellationToken);
            return await uow.CreatePerson(user: FieldValue.Null, cancellationToken: ct);
        });

        var partyUuid = party.PartyUuid.Value;
        var msg = new UpsertPartyUserCommand
        {
            PartyUuid = partyUuid,
            User = new PartyUserRecord { UserIds = ImmutableValueArray.Create(1U) },
            Tracking = new("test", 1),
        };

        await CommandSender.Send(msg, TestContext.Current.CancellationToken);
        var conversation = await TestHarness.Conversation(msg, TestContext.Current.CancellationToken);

        var evt = await conversation.Events.OfType<PartyUpdatedEvent>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        evt.ShouldNotBeNull();
        evt.Party.PartyUuid.ShouldBe(partyUuid);

        await Check(async (uow, ct) =>
        {
            var persistence = uow.GetPartyPersistence();
            var fromDb = await persistence.GetPartyById(partyUuid, PartyFieldIncludes.UserId, ct).FirstAsync(ct);

            fromDb.ShouldNotBeNull();
            fromDb.User.HasValue.ShouldBeTrue();
            fromDb.User.Value.UserId.HasValue.ShouldBeTrue();
            fromDb.User.Value.UserId.Value.ShouldBe(1U);
        });
    }

    private static Func<ExternalRoleAssignmentAddedEvent, bool> MatchAddedRole(PartyRecord from, PartyRecord to, ExternalRoleSource roleSource, string roleIdentifier)
    {
        return e => e.From.PartyUuid == from.PartyUuid.Value
            && e.To.PartyUuid == to.PartyUuid.Value
            && e.Role == new ExternalRoleReference(roleSource, roleIdentifier);
    }

    private static Func<ExternalRoleAssignmentRemovedEvent, bool> MatchRemovedRole(PartyRecord from, PartyRecord to, ExternalRoleSource roleSource, string roleIdentifier)
    {
        return e => e.From.PartyUuid == from.PartyUuid.Value
            && e.To.PartyUuid == to.PartyUuid.Value
            && e.Role == new ExternalRoleReference(roleSource, roleIdentifier);
    }
}
