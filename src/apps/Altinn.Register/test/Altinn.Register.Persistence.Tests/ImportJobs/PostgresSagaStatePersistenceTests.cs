using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Persistence.ImportJobs;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Persistence.Tests.ImportJobs;

public class PostgresSagaStatePersistenceTests
    : DatabaseTestBase
{
    protected override bool SeedData => false;

    private IUnitOfWorkManager? _manager;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _manager = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact]
    public async Task GetState_New_Returns_NullState()
    {
        var sagaId = Guid.NewGuid();

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);
            state.SagaId.Should().Be(sagaId);
            state.Status.Should().Be(SagaStatus.InProgress);
            state.Messages.Should().BeEmpty();
            state.Data.Should().BeNull();
        });
    }

    [Fact]
    public async Task GetState_Existing_ReturnsState()
    {
        var sagaId = Guid.NewGuid();

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);
            state.Messages.Add(sagaId);
            state.Data = new PersonState
            {
                Name = "Alice",
                Age = 30,
            };

            await persistence.SaveState(state);
        });

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);
            
            state.SagaId.Should().Be(sagaId);
            state.Status.Should().Be(SagaStatus.InProgress);
            state.Messages.Should().ContainSingle().Which.Should().Be(sagaId);
            state.Data.Should().NotBeNull();
            state.Data.Name.Should().Be("Alice");
            state.Data.Age.Should().Be(30);
        });
    }

    [Fact]
    public async Task SaveState_CanUpdate_ExistingState()
    {
        var sagaId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);
            state.Messages.Add(sagaId);
            state.Data = new PersonState
            {
                Name = "Alice",
                Age = 30,
            };

            await persistence.SaveState(state);
        });

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);

            state.Messages.Add(messageId);
            state.Status = SagaStatus.Completed;
            state.Data = state.Data! with { Age = 31 };
            await persistence.SaveState(state);
        });

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);

            state.SagaId.Should().Be(sagaId);
            state.Status.Should().Be(SagaStatus.Completed);
            state.Messages.Should().HaveCount(2);
            state.Messages.Should().Contain([sagaId, messageId]);
            state.Data.Should().NotBeNull();
            state.Data.Name.Should().Be("Alice");
            state.Data.Age.Should().Be(31);
        });
    }

    [Fact]
    public async Task DeleteState_Deletes_State()
    {
        var sagaId = Guid.NewGuid();

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);
            state.Data = new PersonState
            {
                Name = "Alice",
                Age = 30,
            };

            await persistence.SaveState(state);
        });

        await Unit(async persistence =>
        {
            await persistence.DeleteState(sagaId);
        });

        await Unit(async persistence =>
        {
            var state = await persistence.GetState<PersonState>(sagaId);
            state.SagaId.Should().Be(sagaId);
            state.Status.Should().Be(SagaStatus.InProgress);
            state.Messages.Should().BeEmpty();
            state.Data.Should().BeNull();
        });
    }

    [Fact]
    public async Task DeleteOldStates_Deletes_Old_States()
    {
        var completed = Guid.CreateVersion7();
        var faulted = Guid.CreateVersion7();
        var inProgress = Guid.CreateVersion7();

        await Unit(async persistence =>
        {
            var completedState = await persistence.GetState<PersonState>(completed);
            completedState.Data = new PersonState { Age = 30, Name = "Alice" };
            completedState.Status = SagaStatus.Completed;
            completedState.Messages.Add(completed);
            await persistence.SaveState(completedState);

            var faultedState = await persistence.GetState<PersonState>(faulted);
            faultedState.Data = new PersonState { Age = 40, Name = "Bob" };
            faultedState.Status = SagaStatus.Faulted;
            faultedState.Messages.Add(faulted);
            await persistence.SaveState(faultedState);

            var inProgressState = await persistence.GetState<PersonState>(inProgress);
            inProgressState.Data = new PersonState { Age = 50, Name = "Charlie" };
            inProgressState.Status = SagaStatus.InProgress;
            inProgressState.Messages.Add(inProgress);
            await persistence.SaveState(inProgressState);
        });

        // first, we delete old states, even though there are none
        await DeleteOldStates(expectedDeleted: 0);
        await CheckNotDeleted([completed, faulted, inProgress]);

        // advance time by 8 days, so the completed state should be old enough to delete
        TimeProvider.Advance(TimeSpan.FromDays(8));
        await DeleteOldStates(expectedDeleted: 1);
        await CheckDeleted(completed);
        await CheckNotDeleted([faulted, inProgress]);

        // advance time by 23 days (31 days total), so the faulted state should be old enough to delete
        TimeProvider.Advance(TimeSpan.FromDays(23));
        await DeleteOldStates(expectedDeleted: 1);
        await CheckDeleted(faulted);
        await CheckNotDeleted([inProgress]);

        // advance time by 60 days (91 days total), so the in-progress state should be old enough to delete
        TimeProvider.Advance(TimeSpan.FromDays(60));
        await DeleteOldStates(expectedDeleted: 1);
        await CheckDeleted(inProgress);

        async Task DeleteOldStates(int expectedDeleted)
        {
            await Unit(async persistence =>
            {
                var now = TimeProvider.GetUtcNow();
                var completedCutoff = now - TimeSpan.FromDays(7);
                var faultedCutoff = now - TimeSpan.FromDays(30);
                var inProgressCutoff = now - TimeSpan.FromDays(90);

                var deleted = await persistence.DeleteOldStates(
                    completedBefore: completedCutoff,
                    faultedBefore: faultedCutoff,
                    inProgressBefore: inProgressCutoff);

                deleted.Should().Be(expectedDeleted);
            });
        }

        async Task CheckDeleted(Guid sagaId)
        {
            await Unit(async persistence =>
            {
                var state = await persistence.GetState<PersonState>(sagaId);
                state.SagaId.Should().Be(sagaId);
                state.Status.Should().Be(SagaStatus.InProgress);
                state.Messages.Should().BeEmpty();
                state.Data.Should().BeNull();
            });
        }

        async Task CheckNotDeleted(IEnumerable<Guid> sagaIds)
        {
            await Unit(async persistence =>
            {
                foreach (var sagaId in sagaIds)
                {
                    var state = await persistence.GetState<PersonState>(sagaId);
                    state.SagaId.Should().Be(sagaId);
                    state.Messages.Should().ContainSingle().Which.Should().Be(sagaId);
                    state.Data.Should().NotBeNull();
                }
            });
        }
    }

    private async Task Unit(Func<PostgresSagaStatePersistence, Task> action, [CallerMemberName] string name = "")
    {
        await using var uow = await _manager!.CreateAsync(activityName: name);
        var persistence = uow.GetRequiredService<PostgresSagaStatePersistence>();
        await action(persistence);

        await uow.CommitAsync();
    }

    private sealed record class PersonState
        : ISagaStateData<PersonState>
    {
        public static string StateType => nameof(PersonState);

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("age")]
        public required uint Age { get; set; }
    }
}
