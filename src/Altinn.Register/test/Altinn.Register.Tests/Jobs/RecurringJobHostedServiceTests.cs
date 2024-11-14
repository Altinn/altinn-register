#nullable enable

using Altinn.Register.Jobs;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Tests.Jobs;

public class RecurringJobHostedServiceTests
    : DatabaseTestBase
{
    private static readonly ObjectFactory<RecurringJobHostedService> _factory
        = ActivatorUtilities.CreateFactory<RecurringJobHostedService>([typeof(IEnumerable<JobRegistration>)]);

    protected override bool SeedData => false;

    protected override async ValueTask ConfigureServices(IServiceCollection services)
    {
        await base.ConfigureServices(services);

        services.AddLeaseManager();
    }

    [Fact]
    public async Task CanRun_WithNo_JobRegistrations()
    {
        using var sut = CreateService([]);

        await Start(sut);
        await Stop(sut);
    }

    [Fact]
    public async Task CanRun_JobAt_Starting()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([CounterRegistration.RunAt(JobHostLifecycles.Starting, counter)]);

        counter.Value.Should().Be(0);
        await sut.StartingAsync(CancellationToken.None);
        counter.Value.Should().Be(1);

        await sut.StartAsync(CancellationToken.None);
        await sut.StartedAsync(CancellationToken.None);
        await sut.StoppingAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);
        await sut.StoppedAsync(CancellationToken.None);

        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task CanRun_JobAt_Start()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([CounterRegistration.RunAt(JobHostLifecycles.Start, counter)]);

        await sut.StartingAsync(CancellationToken.None);

        counter.Value.Should().Be(0);
        await sut.StartAsync(CancellationToken.None);
        counter.Value.Should().Be(1);

        await sut.StartedAsync(CancellationToken.None);
        await sut.StoppingAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);
        await sut.StoppedAsync(CancellationToken.None);

        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task CanRun_JobAt_Started()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([CounterRegistration.RunAt(JobHostLifecycles.Started, counter)]);

        await sut.StartingAsync(CancellationToken.None);
        await sut.StartAsync(CancellationToken.None);

        counter.Value.Should().Be(0);
        await sut.StartedAsync(CancellationToken.None);
        counter.Value.Should().Be(1);

        await sut.StoppingAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);
        await sut.StoppedAsync(CancellationToken.None);

        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task CanRun_JobAt_Stopping()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([CounterRegistration.RunAt(JobHostLifecycles.Stopping, counter)]);

        await sut.StartingAsync(CancellationToken.None);
        await sut.StartAsync(CancellationToken.None);
        await sut.StartedAsync(CancellationToken.None);

        counter.Value.Should().Be(0);
        await sut.StoppingAsync(CancellationToken.None);
        counter.Value.Should().Be(1);

        await sut.StopAsync(CancellationToken.None);
        await sut.StoppedAsync(CancellationToken.None);

        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task CanRun_JobAt_Stop()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([CounterRegistration.RunAt(JobHostLifecycles.Stop, counter)]);

        await sut.StartingAsync(CancellationToken.None);
        await sut.StartAsync(CancellationToken.None);
        await sut.StartedAsync(CancellationToken.None);
        await sut.StoppingAsync(CancellationToken.None);

        counter.Value.Should().Be(0);
        await sut.StopAsync(CancellationToken.None);
        counter.Value.Should().Be(1);

        await sut.StoppedAsync(CancellationToken.None);

        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task CanRun_JobAt_Stopped()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([CounterRegistration.RunAt(JobHostLifecycles.Stopped, counter)]);

        await sut.StartingAsync(CancellationToken.None);
        await sut.StartAsync(CancellationToken.None);
        await sut.StartedAsync(CancellationToken.None);
        await sut.StoppingAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        counter.Value.Should().Be(0);
        await sut.StoppedAsync(CancellationToken.None);
        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task CanRun_JobAt_All()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([CounterRegistration.RunAt(
            JobHostLifecycles.Starting | JobHostLifecycles.Start | JobHostLifecycles.Started | JobHostLifecycles.Stopping | JobHostLifecycles.Stop | JobHostLifecycles.Stopped,
            counter)]);

        counter.Value.Should().Be(0);
        await sut.StartingAsync(CancellationToken.None);
        counter.Value.Should().Be(1);
        await sut.StartAsync(CancellationToken.None);
        counter.Value.Should().Be(2);
        await sut.StartedAsync(CancellationToken.None);
        counter.Value.Should().Be(3);
        await sut.StoppingAsync(CancellationToken.None);
        counter.Value.Should().Be(4);
        await sut.StopAsync(CancellationToken.None);
        counter.Value.Should().Be(5);
        await sut.StoppedAsync(CancellationToken.None);
        counter.Value.Should().Be(6);
    }

    private RecurringJobHostedService CreateService(IEnumerable<JobRegistration> registrations)
        => _factory(Services, [registrations]);

    private static async Task Start(RecurringJobHostedService service, CancellationToken cancellationToken = default)
    {
        await service.StartingAsync(cancellationToken);
        await service.StartAsync(cancellationToken);
        await service.StartedAsync(cancellationToken);
    }

    private static async Task Stop(RecurringJobHostedService service, CancellationToken cancellationToken = default)
    {
        await service.StoppingAsync(cancellationToken);
        await service.StopAsync(cancellationToken);
        await service.StoppedAsync(cancellationToken);
    }

    private sealed class CounterJob(AtomicCounter counter)
        : IJob
    {
        public Task RunAsync(CancellationToken cancellationToken)
        {
            counter.Increment();

            return Task.CompletedTask;
        }
    }

    private sealed class CounterRegistration(AtomicCounter counter, string? leaseName, TimeSpan interval, JobHostLifecycles runAt)
        : JobRegistration(leaseName, interval, runAt)
    {
        public static new CounterRegistration RunAt(JobHostLifecycles runAt, AtomicCounter counter)
            => new CounterRegistration(counter, null, TimeSpan.Zero, runAt);

        public override IJob Create(IServiceProvider services)
            => new CounterJob(counter);
    }
}
