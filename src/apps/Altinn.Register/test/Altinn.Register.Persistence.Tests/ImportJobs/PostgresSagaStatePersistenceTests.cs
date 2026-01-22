using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
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

    private async Task Unit(Func<ISagaStatePersistence, Task> action, [CallerMemberName] string name = "")
    {
        await using var uow = await _manager!.CreateAsync(activityName: name);
        var persistence = uow.GetRequiredService<ISagaStatePersistence>();
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
