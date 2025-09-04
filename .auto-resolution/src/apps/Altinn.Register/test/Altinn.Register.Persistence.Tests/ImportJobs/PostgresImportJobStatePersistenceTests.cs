using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Persistence.Tests.ImportJobs;

public class PostgresImportJobStatePersistenceTests
    : DatabaseTestBase
{
    protected override bool SeedData => false;

    private IUnitOfWorkManager? _manager;
    private IUnitOfWork? _uow;
    private IImportJobStatePersistence? _persistence;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _manager = GetRequiredService<IUnitOfWorkManager>();
        _uow = await _manager.CreateAsync(activityName: "test");
        _persistence = _uow.GetRequiredService<IImportJobStatePersistence>();
    }

    protected override async ValueTask DisposeAsync()
    {
        if (_uow is { } uow)
        {
            await uow.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private IUnitOfWork UoW
        => _uow!;

    private IImportJobStatePersistence Persistence
        => _persistence!;

    [Fact]
    public async Task CanRoundTrip_StringState()
    {
        var party = await UoW.CreateOrg();
        var jobId = "test";

        var partyUuid = party.PartyUuid.Value;

        var state = new StringState { Value = "test" };
        await Persistence.SetPartyState(jobId, partyUuid, state);

        var result = await Persistence.GetPartyState<StringState>(jobId, partyUuid);
        result.Should().HaveValue()
            .Which.Value.Should().Be(state.Value);
    }

    [Fact]
    public async Task CanRoundTrip_IntState()
    {
        var party = await UoW.CreateOrg();
        var jobId = "test";

        var partyUuid = party.PartyUuid.Value;

        var state = new IntState { Value = 42 };
        await Persistence.SetPartyState(jobId, partyUuid, state);

        var result = await Persistence.GetPartyState<IntState>(jobId, partyUuid);
        result.Should().HaveValue()
            .Which.Value.Should().Be(state.Value);
    }

    [Fact]
    public async Task GetPartyState_ReturnsUnset_WhenMissing()
    {
        var result = await Persistence.GetPartyState<StringState>("test", Guid.NewGuid());

        result.Should().BeUnset();
    }

    [Fact]
    public async Task GetPartyState_ReturnsNull_WhenWrongStateType()
    {
        var party = await UoW.CreateOrg();
        var jobId = "test";

        var partyUuid = party.PartyUuid.Value;

        var state = new StringState { Value = "test" };
        await Persistence.SetPartyState(jobId, partyUuid, state);

        var result = await Persistence.GetPartyState<IntState>(jobId, partyUuid);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearState_ClearsState()
    {
        var party = await UoW.CreateOrg();
        var jobId = "test";

        var partyUuid = party.PartyUuid.Value;

        var state = new StringState { Value = "test" };
        await Persistence.SetPartyState(jobId, partyUuid, state);
        var result = await Persistence.GetPartyState<StringState>(jobId, partyUuid);
        result.Should().NotBeNull();

        await Persistence.ClearPartyState(jobId, partyUuid);

        result = await Persistence.GetPartyState<StringState>(jobId, partyUuid);
        result.Should().BeUnset();
    }

    private sealed class StringState
        : IImportJobState<StringState>
    {
        public static string StateType => nameof(StringState);

        public required string Value { get; init; }
    }

    private sealed class IntState
        : IImportJobState<IntState>
    {
        public static string StateType => nameof(IntState);
        
        public required int Value { get; init; }
    }
}
