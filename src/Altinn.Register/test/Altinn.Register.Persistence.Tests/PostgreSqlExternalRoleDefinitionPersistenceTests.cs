using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.TestUtils;

namespace Altinn.Register.Persistence.Tests;

public class PostgreSqlExternalRoleDefinitionPersistenceTests
    : DatabaseTestBase
{
    protected override bool SeedData => false;

    private PostgreSqlExternalRoleDefinitionPersistence? _persistence;

    private PostgreSqlExternalRoleDefinitionPersistence Persistence => _persistence!;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _persistence = GetRequiredService<PostgreSqlExternalRoleDefinitionPersistence>();
    }

    [Theory]
    [InlineData(PartySource.CentralCoordinatingRegister, "bedr")]
    [InlineData(PartySource.CentralCoordinatingRegister, "aafy")]
    public async Task GetsRoleDefinition(PartySource source, string identifier)
    {
        var def = await Persistence.TryGetRoleDefinition(source, identifier);
        Assert.NotNull(def);
        Assert.Equal(source, def.Source);
        Assert.Equal(identifier, def.Identifier);
    }

    [Theory]
    [InlineData("BEDR", PartySource.CentralCoordinatingRegister, "bedr")]
    [InlineData("AAFY", PartySource.CentralCoordinatingRegister, "aafy")]
    public async Task GetsRoleDefinition_ByRoleCode(string roleCode, PartySource source, string identifier)
    {
        var def = await Persistence.TryGetRoleDefinitionByRoleCode(roleCode);
        Assert.NotNull(def);
        Assert.Equal(source, def.Source);
        Assert.Equal(identifier, def.Identifier);
    }

    [Fact]
    public async Task HandlesThreadSpam()
    {
        var evt = new ManualResetEventSlim(initialState: false);

        var tasks = Enumerable.Range(0, 100)
            .Select(_ =>
            {
                var tcs = new TaskCompletionSource();
                var thread = new Thread(() =>
                {
                    try
                    {
                        evt.Wait();
                        var vtask = Persistence.TryGetRoleDefinition(PartySource.CentralCoordinatingRegister, "bedr");
                        CompleteVTask(tcs, vtask);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                thread.Start();
                return tcs.Task;
            })
            .ToArray();

        evt.Set();
        await Task.WhenAll(tasks);

        static void CompleteVTask(TaskCompletionSource tcs, ValueTask<ExternalRoleDefinition?> vtask)
        {
            if (vtask.IsCompleted)
            {
                CompleteCompletedVTask(tcs, vtask);
                return;
            }
            
            var awaiter = vtask.GetAwaiter();
            awaiter.UnsafeOnCompleted(() =>
            {
                CompleteVTask(tcs, vtask);
            });
        }

        static void CompleteCompletedVTask(TaskCompletionSource tsc, ValueTask<ExternalRoleDefinition?> vtask)
        {
            try
            {
                var result = vtask.GetAwaiter().GetResult();
                Assert.NotNull(result);
                Assert.Equal(PartySource.CentralCoordinatingRegister, result.Source);
                Assert.Equal("bedr", result.Identifier);
            }
            catch (Exception ex)
            {
                tsc.TrySetException(ex);
            }
            finally
            {
                tsc.TrySetResult();
            }
        }
    }
}
