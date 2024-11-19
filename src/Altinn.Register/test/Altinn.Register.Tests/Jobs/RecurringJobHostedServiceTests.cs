#nullable enable

using Altinn.Register.Core;
using Altinn.Register.Core.Leases;
using Altinn.Register.Jobs;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Altinn.Register.Tests.Jobs;

public class RecurringJobHostedServiceTests
    : DatabaseTestBase
{
    private static readonly ObjectFactory<RecurringJobHostedService> _factory
        = ActivatorUtilities.CreateFactory<RecurringJobHostedService>([typeof(IEnumerable<JobRegistration>)]);

    protected override bool SeedData => false;

    private ILeaseProvider? _provider;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _provider = GetRequiredService<ILeaseProvider>();
    }

    protected override async ValueTask ConfigureServices(IServiceCollection services)
    {
        await base.ConfigureServices(services);

        services.AddLeaseManager();
        services.TryAddSingleton<RegisterTelemetry>();
    }

    private ILeaseProvider Provider
        => _provider!;

    [Fact]
    public async Task CanRun_WithNo_JobRegistrations()
    {
        using var sut = CreateService([]);

        await Run(sut);
    }

    [Fact]
    public async Task CanRun_JobAt_Starting()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Starting, counter)]);

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
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Start, counter)]);

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
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Started, counter)]);

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
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopping, counter)]);

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
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stop, counter)]);

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
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopped, counter)]);

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
        using var sut = CreateService([Counter.RunAt(
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

    [Fact]
    public async Task Cannot_UseLease_At_Starting()
    {
        var counter = new AtomicCounter();
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Starting, "test", counter)]);

        await sut.Invoking(s => s.StartingAsync(CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Can_UseLease_At_Start()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Start, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), _ => false);
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Started()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Started, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), _ => false);
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Stopping()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopping, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), _ => false);
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Stop()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stop, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), _ => false);
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Stopped()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopped, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), _ => false);
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Theory]
    [InlineData(JobHostLifecycles.Starting)]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task LifecycleJobs_Propagate_Errors(JobHostLifecycles lifecycle)
    {
        using var sut = CreateService([Registration.RunAt(lifecycle, new Func<IServiceProvider, Task>(_ => throw new InvalidOperationException("I died miserably")))]);

        var assert = await sut.Invoking(s => Run(s)).Should().ThrowExactlyAsync<InvalidOperationException>();
        assert.WithMessage("I died miserably");
    }

    [Theory]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task Leased_LifecycleJobs_IsSkipped_WhenLease_IsTaken(JobHostLifecycles lifecycle)
    {
        var counter = new AtomicCounter();

        await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        using var sut = CreateService([Counter.RunAt(lifecycle, "test", counter)]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task DisposableJobs_AreDisposed()
    {
        var job1Dispose = new AtomicCounter();
        var job2DisposeAsync = new AtomicCounter();
        var job3Dispose = new AtomicCounter();
        var job3DisposeAsync = new AtomicCounter();

        using var sut = CreateService([
            Registration.RunAt(JobHostLifecycles.Start, _ => new DisposableJob(job1Dispose)),
            Registration.RunAt(JobHostLifecycles.Start, _ => new AsyncDisposableJob(job2DisposeAsync)),
            Registration.RunAt(JobHostLifecycles.Start, _ => new BothDisposableJob(job3Dispose, job3DisposeAsync)),
        ]);

        await Run(sut);

        job1Dispose.Value.Should().Be(1);
        job2DisposeAsync.Value.Should().Be(1);

        // async dispose is prioritized over sync dispose
        job3Dispose.Value.Should().Be(0);
        job3DisposeAsync.Value.Should().Be(1);
    }

    [Fact]
    public async Task ScheduledJobs_WithoutLease_AreNotRunImmediately()
    {
        var counter = new AtomicCounter();

        using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), counter),
        ]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task ScheduledJobs_RunMultipleTimes()
    {
        var counter = new AtomicCounter();

        using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), counter),
        ]);

        await Start(sut);

        for (var i = 0; i < 10; i++)
        {
            TimeProvider.Advance(TimeSpan.FromHours(1));
            await sut.WaitForRunningScheduledJobs();
        }

        await Stop(sut);

        counter.Value.Should().Be(10);
    }

    [Fact]
    public async Task ScheduledJobs_Interval_IsBetweenRuns()
    {
        var counter = new AtomicCounter();

        using var sut = CreateService([
            Registration.Scheduled(
                TimeSpan.FromHours(1),
                services =>
                {
                    var timeProvider = services.GetRequiredService<FakeTimeProvider>();

                    counter.Increment();
                    
                    // this job took 10 minutes
                    timeProvider.Advance(TimeSpan.FromMinutes(10));

                    return Task.CompletedTask;
                }),
        ]);

        await Start(sut);

        var start = TimeProvider.GetUtcNow();
        TimeProvider.SetUtcNow(start + TimeSpan.FromHours(1));
        await sut.WaitForRunningScheduledJobs();

        var afterFirst = TimeProvider.GetUtcNow();
        counter.Value.Should().Be(1);

        TimeProvider.SetUtcNow(start + TimeSpan.FromHours(2));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(1);

        TimeProvider.SetUtcNow(afterFirst + TimeSpan.FromHours(1));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(2);

        await Stop(sut);
    }

    [Fact]
    public async Task ScheduledJobs_WithLease_RunsImmediately()
    {
        var counter = new AtomicCounter();

        using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), "test", counter),
        ]);

        await Run(sut);

        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task ScheduledJobs_WithLease_ThatRanRecently_AreNotRunImmediately()
    {
        var result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result.IsLeaseAcquired);
        await Provider.ReleaseLease(result.Lease);

        var counter = new AtomicCounter();

        using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), "test", counter),
        ]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task ScheduledJobs_WithLease_UsesLeaseTime_ToScheduleRuns()
    {
        var result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result.IsLeaseAcquired);
        await Provider.ReleaseLease(result.Lease);

        // advance 40 minutes since the lease was released
        TimeProvider.Advance(TimeSpan.FromMinutes(40));

        var counter = new AtomicCounter();

        using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), "test", counter),
        ]);

        await Start(sut);
        counter.Value.Should().Be(0);

        // advance 20 minutes, 1 hour since the lease was released
        TimeProvider.Advance(TimeSpan.FromMinutes(20));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(1);
        result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), _ => false);
        result.LastReleasedAt.Should().Be(TimeProvider.GetUtcNow());

        // advance 40 more minutes and renew the lease
        TimeProvider.Advance(TimeSpan.FromMinutes(40));
        result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result.IsLeaseAcquired);
        await Provider.ReleaseLease(result.Lease);

        // advance 20 more minutes, 1 hour since the job last completed
        TimeProvider.Advance(TimeSpan.FromMinutes(20));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(1);

        // advance 40 more minutes, 1 hour since the lease was last released
        TimeProvider.Advance(TimeSpan.FromMinutes(40));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(2);
        result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1), _ => false);
        result.LastReleasedAt.Should().Be(TimeProvider.GetUtcNow());
    }

    private RecurringJobHostedService CreateService(IEnumerable<JobRegistration> registrations)
        => _factory(Services, [registrations]);

    private static async Task Start(RecurringJobHostedService service, CancellationToken cancellationToken = default)
    {
        await service.StartingAsync(cancellationToken);
        await service.StartAsync(cancellationToken);
        await service.StartedAsync(cancellationToken);
        await service.WaitForRunningScheduledJobs();
    }

    private static async Task Stop(RecurringJobHostedService service, CancellationToken cancellationToken = default)
    {
        await service.StoppingAsync(cancellationToken);
        await service.StopAsync(cancellationToken);
        await service.StoppedAsync(cancellationToken);
    }

    private static async Task Run(RecurringJobHostedService service, CancellationToken cancellationToken = default)
    {
        await Start(service, cancellationToken);
        await Stop(service, cancellationToken);
    }

    private sealed class DelegateJob(Func<IServiceProvider, Task> run, IServiceProvider services)
        : IJob
    {
        public Task RunAsync(CancellationToken cancellationToken)
            => run(services);
    }

    private sealed class Registration(Func<IServiceProvider, IJob> job, string? leaseName, TimeSpan interval, JobHostLifecycles runAt)
        : JobRegistration(leaseName, interval, runAt)
    {
        public static JobRegistration Create(Func<IServiceProvider, IJob> job, string? leaseName, TimeSpan interval, JobHostLifecycles runAt)
            => new Registration(job, leaseName, interval, runAt);

        public static JobRegistration Create(Func<IServiceProvider, Task> job, string? leaseName, TimeSpan interval, JobHostLifecycles runAt)
            => new Registration(services => new DelegateJob(job, services), leaseName, interval, runAt);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, Func<IServiceProvider, IJob> job)
            => Create(job, null, TimeSpan.Zero, runAt);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, Func<IServiceProvider, Task> job)
            => Create(job, null, TimeSpan.Zero, runAt);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, string leaseName, Func<IServiceProvider, IJob> job)
            => Create(job, leaseName, TimeSpan.Zero, runAt);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, string leaseName, Func<IServiceProvider, Task> job)
            => Create(job, leaseName, TimeSpan.Zero, runAt);

        public static JobRegistration Scheduled(TimeSpan interval, Func<IServiceProvider, Task> job)
            => Create(job, null, interval, JobHostLifecycles.None);

        public override IJob Create(IServiceProvider services)
            => job(services);
    }

    private static class Counter
    {
        public static JobRegistration Create(AtomicCounter counter, string? leaseName, TimeSpan interval, JobHostLifecycles runAt)
        {
            var job = (IServiceProvider services) =>
            {
                var timeProvider = services.GetRequiredService<FakeTimeProvider>();

                counter.Increment();
                timeProvider.Advance(TimeSpan.FromSeconds(10));

                return Task.CompletedTask;
            };

            return Registration.Create(job, leaseName, interval, runAt);
        }

        public static JobRegistration RunAt(JobHostLifecycles runAt, AtomicCounter counter)
            => Create(counter, null, TimeSpan.Zero, runAt);

        public static JobRegistration RunAt(JobHostLifecycles runAt, string leaseName, AtomicCounter counter)
            => Create(counter, leaseName, TimeSpan.Zero, runAt);

        public static JobRegistration Scheduled(TimeSpan interval, AtomicCounter counter)
            => Create(counter, null, interval, JobHostLifecycles.None);

        public static JobRegistration Scheduled(TimeSpan interval, string leaseName, AtomicCounter counter)
            => Create(counter, leaseName, interval, JobHostLifecycles.None);
    }

    private sealed class DisposableJob(AtomicCounter disposeCounter)
        : IJob
        , IDisposable
    {
        public void Dispose()
        {
            disposeCounter.Increment();
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncDisposableJob(AtomicCounter disposeCounter)
        : IJob
        , IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            disposeCounter.Increment();

            return ValueTask.CompletedTask;
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BothDisposableJob(AtomicCounter disposeCounter, AtomicCounter asyncDisposeCounter)
        : IJob
        , IAsyncDisposable
    {
        public void Dispose()
        {
            disposeCounter.Increment();
        }

        public ValueTask DisposeAsync()
        {
            asyncDisposeCounter.Increment();

            return ValueTask.CompletedTask;
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
