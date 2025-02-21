#nullable enable

using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.MassTransit;
using Altinn.Register.TestUtils.TestData;
using Xunit.Abstractions;

namespace Altinn.Register.Tests.PartyImport;

public class PartyImportBatchConsumerTests(ITestOutputHelper output)
    : BusTestBase(output)
{
    protected override bool SeedData => false;

    ////protected override ITestOutputHelper? TestOutputHelper => output;

    [Fact]
    public async Task UpsertExternalRoleAssignmentsCommand_UpsertsExternalRoleAssignments()
    {
        await GetRequiredService<IImportJobTracker>().TrackQueueStatus("test", new()
        {
            SourceMax = 10,
            EnqueuedMax = 1,
        });
        var (org, person1, person2) = await Setup(async uow =>
        {
            var org = await uow.CreateOrg();
            var person1 = await uow.CreatePerson();
            var person2 = await uow.CreatePerson();

            var roles = uow.GetPartyExternalRolePersistence();
            await roles.UpsertExternalRolesFromPartyBySource(
                commandId: Guid.CreateVersion7(),
                partyUuid: org.PartyUuid.Value,
                roleSource: ExternalRoleSource.CentralCoordinatingRegister,
                assignments: [
                    new("styreleder", person2.PartyUuid.Value),
                ]);

            return (org, person1, person2);
        });

        var cmd = new UpsertExternalRoleAssignmentsCommand
        {
            FromPartyUuid = org.PartyUuid.Value,
            Source = ExternalRoleSource.CentralCoordinatingRegister,
            Tracking = new("test", 1),
            Assignments = [
                new() { ToPartyUuid = person1.PartyUuid.Value, Identifier = "daglig-leder" },
                new() { ToPartyUuid = person1.PartyUuid.Value, Identifier = "styremedlem" },
                new() { ToPartyUuid = person2.PartyUuid.Value, Identifier = "styremedlem" },
            ],
        };

        await CommandSender.Send(cmd);
        var consumed = await Harness.Consumed.SelectAsync<UpsertExternalRoleAssignmentsCommand>(m => m.Context.CorrelationId == cmd.CommandId).FirstAsync();
        var conversation = Harness.Conversation(consumed.Context.ConversationId!.Value);

        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "daglig-leder")));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem")));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem")));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentRemovedEvent>().AnyAsync(MatchRemovedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styreleder")));

        // idempotency check
        await CommandSender.Send(cmd);
        var consumed2 = await Harness.Consumed.SelectAsync<UpsertExternalRoleAssignmentsCommand>(m => m.Context.CorrelationId == cmd.CommandId && m.Context.ConversationId != consumed.Context.ConversationId).FirstAsync();

        conversation = Harness.Conversation(consumed2.Context.ConversationId!.Value);

        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "daglig-leder")));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person1, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem")));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentAddedEvent>().AnyAsync(MatchAddedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styremedlem")));
        Assert.True(await conversation.Events.OfType<ExternalRoleAssignmentRemovedEvent>().AnyAsync(MatchRemovedRole(org, person2, ExternalRoleSource.CentralCoordinatingRegister, "styreleder")));
    }

    private async Task<T> Setup<T>(Func<IUnitOfWork, Task<T>> setup)
    {
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync(activityName: $"{nameof(PartyImportBatchConsumerTests)}.{nameof(Setup)}");
        var result = await setup(uow);
        await uow.CommitAsync();

        return result;
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
