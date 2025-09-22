#nullable enable

using Altinn.Register.Contracts;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.PartyImport;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.MassTransit;
using Altinn.Register.TestUtils.TestData;
using Xunit.Abstractions;

namespace Altinn.Register.Tests.PartyImport.A2;

public class A2ExternalRoleResolverConsumerTests(ITestOutputHelper output)
    : BusTestBase(output)
{
    protected override bool SeedData => false;

    ////protected override ITestOutputHelper? TestOutputHelper => output;

    [Fact]
    public async Task ResolveAndUpsertA2CCRRoleAssignmentsCommand_LooksUpRoles_AndUpserts()
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

        var cmd = new ResolveAndUpsertA2CCRRoleAssignmentsCommand 
        {
            FromPartyUuid = org.PartyUuid.Value,
            Tracking = new("test", 1),
            RoleAssignments = [
                new() { ToPartyUuid = person1.PartyUuid.Value, RoleCode = "DAGL" },
                new() { ToPartyUuid = person1.PartyUuid.Value, RoleCode = "MEDL" },
                new() { ToPartyUuid = person2.PartyUuid.Value, RoleCode = "MEDL" },
                new() { ToPartyUuid = person2.PartyUuid.Value, RoleCode = "NON_EXISTING" },
            ],
        };

        await CommandSender.Send(cmd);
        var conversation = await Harness.Conversation(cmd);

        var upsertCommand = await conversation.Commands.OfType<UpsertExternalRoleAssignmentsCommand>().FirstOrDefaultAsync();
        Assert.NotNull(upsertCommand);

        Assert.Equal(ExternalRoleSource.CentralCoordinatingRegister, upsertCommand.Source);
        Assert.Equal(org.PartyUuid.Value, upsertCommand.FromPartyUuid);
        Assert.Equal(cmd.Tracking, upsertCommand.Tracking);
        Assert.Collection(
            upsertCommand.Assignments,
            a =>
            {
                Assert.Equal(person1.PartyUuid.Value, a.ToPartyUuid);
                Assert.Equal("daglig-leder", a.Identifier);
            },
            a =>
            {
                Assert.Equal(person1.PartyUuid.Value, a.ToPartyUuid);
                Assert.Equal("styremedlem", a.Identifier);
            },
            a =>
            {
                Assert.Equal(person2.PartyUuid.Value, a.ToPartyUuid);
                Assert.Equal("styremedlem", a.Identifier);
            });
    }

    private async Task<T> Setup<T>(Func<IUnitOfWork, Task<T>> setup)
    {
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync(activityName: $"{GetType().Name}.{nameof(Setup)}");
        var result = await setup(uow);
        await uow.CommitAsync();

        return result;
    }
}
