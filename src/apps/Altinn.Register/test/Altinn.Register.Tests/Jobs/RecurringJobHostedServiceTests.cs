#nullable enable

using System.Collections.Immutable;
using System.Data;
using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Authorization.ServiceDefaults.Leases;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Jobs;
using Altinn.Register.Persistence;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Altinn.Register.Tests.Jobs;

public class RecurringJobHostedServiceTests
    : DatabaseTestBase
{
    private static readonly ObjectFactory<RecurringJobHostedService> _factory
        = ActivatorUtilities.CreateFactory<RecurringJobHostedService>([typeof(IEnumerable<JobRegistration>), typeof(IEnumerable<IJobCondition>)]);

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
        services.TryAddSingleton<JobsTelemetry>();
    }

    private ILeaseProvider Provider
        => _provider!;

    [Fact]
    public async Task CanRun_WithNo_JobRegistrations()
    {
        await using var sut = CreateService([]);

        await Run(sut);
    }

    [Fact]
    public async Task CanRun_JobAt_Starting()
    {
        var counter = new AtomicCounter();
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Starting, counter)]);

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
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Start, counter)]);

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
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Started, counter)]);

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
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopping, counter)]);

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
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stop, counter)]);

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
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopped, counter)]);

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
        await using var sut = CreateService([Counter.RunAt(
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
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Starting, "test", counter)]);

        await sut.Invoking(s => s.StartingAsync(CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Can_UseLease_At_Start()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Start, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await GetLeaseInfo("test");
        lease.Should().NotBeNull();
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Started()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Started, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await GetLeaseInfo("test");
        lease.Should().NotBeNull();
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Stopping()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopping, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await GetLeaseInfo("test");
        lease.Should().NotBeNull();
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Stop()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stop, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await GetLeaseInfo("test");
        lease.Should().NotBeNull();
        lease.LastAcquiredAt.Should().Be(start);
        lease.LastReleasedAt.Should().Be(end);
    }

    [Fact]
    public async Task Can_UseLease_At_Stopped()
    {
        var counter = new AtomicCounter();
        var start = TimeProvider.GetUtcNow();
        await using var sut = CreateService([Counter.RunAt(JobHostLifecycles.Stopped, "test", counter)]);

        await Run(sut);
        var end = TimeProvider.GetUtcNow();

        counter.Value.Should().Be(1);
        end.Should().Be(start + TimeSpan.FromSeconds(10));

        var lease = await GetLeaseInfo("test");
        lease.Should().NotBeNull();
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
        await using var sut = CreateService([Registration.RunAt(lifecycle, new Func<IServiceProvider, Task>(_ => throw new InvalidOperationException("I died miserably")), services => ValueTask.FromResult(true))]);

        var assert = await sut.Invoking(s => Run(s)).Should().ThrowExactlyAsync<InvalidOperationException>();
        assert.WithMessage("I died miserably");
    }

    [Theory]
    [InlineData(JobHostLifecycles.Starting)]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task LifecycleJobs_Propagate_ShouldRun_Errors(JobHostLifecycles lifecycle)
    {
        await using var sut = CreateService([Registration.RunAt(lifecycle, new Func<IServiceProvider, Task>(_ => throw new InvalidOperationException("I died miserably")), services => throw new InvalidOperationException("I died miserably (shouldrun)"))]);

        var assert = await sut.Invoking(s => Run(s)).Should().ThrowExactlyAsync<InvalidOperationException>();
        assert.WithMessage("I died miserably (shouldrun)");
    }

    [Theory]
    [InlineData(JobHostLifecycles.Starting)]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task LifecycleJobs_CanBeDisabled(JobHostLifecycles lifecycle)
    {
        var counter = new AtomicCounter();

        await using var sut = CreateService([
            Counter.RunAt(lifecycle, counter, enabled: (_, _) => ValueTask.FromResult(JobShouldRunResult.No("test"))),
        ]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Theory]
    [InlineData(JobHostLifecycles.Starting)]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task LifecycleJobs_CanBeEnabled_ByCondition(JobHostLifecycles lifecycle)
    {
        var condition = new ConstCondition(true, tags: []);
        var counter = new AtomicCounter();

        await using var sut = CreateService(
            [Counter.RunAt(lifecycle, counter)], 
            [condition]);

        await Run(sut);

        counter.Value.Should().Be(1);
    }

    [Theory]
    [InlineData(JobHostLifecycles.Starting)]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task LifecycleJobs_CanBeDisabled_ByCondition(JobHostLifecycles lifecycle)
    {
        var condition = new ConstCondition(false, tags: []);
        var counter = new AtomicCounter();

        await using var sut = CreateService(
            [Counter.RunAt(lifecycle, counter)],
            [condition]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Theory]
    [InlineData(JobHostLifecycles.Starting)]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task LifecycleJobs_Can_WaitFor(JobHostLifecycles lifecycle)
    {
        var counter = new AtomicCounter();

        var tcs = new TaskCompletionSource();
        await using var sut = CreateService([
            Counter.RunAt(lifecycle, counter, tcs.Task),
        ]);

        var task = Run(sut);

        await Task.Yield();
        counter.Value.Should().Be(0);
        task.IsCompleted.Should().BeFalse();

        tcs.SetResult();
        await task;
        counter.Value.Should().Be(1);
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
        await using var sut = CreateService([Counter.RunAt(lifecycle, "test", counter)]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Theory]
    [InlineData(JobHostLifecycles.Start)]
    [InlineData(JobHostLifecycles.Started)]
    [InlineData(JobHostLifecycles.Stopping)]
    [InlineData(JobHostLifecycles.Stop)]
    [InlineData(JobHostLifecycles.Stopped)]
    public async Task Leased_LifecycleJobs_CanBeDisabled(JobHostLifecycles lifecycle)
    {
        var counter = new AtomicCounter();

        var leaseName = "test";
        var initialLeaseInfo = await GetLeaseInfo(leaseName);
        initialLeaseInfo.Expires.Should().Be(DateTimeOffset.MinValue);
        initialLeaseInfo.LastAcquiredAt.Should().BeNull();
        initialLeaseInfo.LastReleasedAt.Should().BeNull();

        await using var sut = CreateService([
            Counter.RunAt(lifecycle, leaseName, counter, enabled: (_, _) => ValueTask.FromResult(JobShouldRunResult.No("test"))),
        ]);

        await Run(sut);

        counter.Value.Should().Be(0);

        var postLeaseInfo = await GetLeaseInfo(leaseName);
        postLeaseInfo.Should().Be(initialLeaseInfo);
    }

    [Fact]
    public async Task DisposableJobs_AreDisposed()
    {
        var job1Dispose = new AtomicCounter();
        var job2DisposeAsync = new AtomicCounter();
        var job3Dispose = new AtomicCounter();
        var job3DisposeAsync = new AtomicCounter();

        await using var sut = CreateService([
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
    public async Task ScheduledJobs_WithoutLease_CanBeDisabled()
    {
        var counter = new AtomicCounter();

        await using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), counter, enabled: (_, _) => ValueTask.FromResult(JobShouldRunResult.No("test"))),
        ]);

        await Start(sut);
        TimeProvider.Advance(TimeSpan.FromHours(1));
        await Stop(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task ScheduledJobs_WithoutLease_AreNotRunImmediately()
    {
        var counter = new AtomicCounter();

        await using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), counter),
        ]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task ScheduledJobs_WithoutLease_CanWaitFor()
    {
        var counter = new AtomicCounter();
        var tcs = new TaskCompletionSource();

        await using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), counter, tcs.Task),
        ]);

        await Start(sut);
        TimeProvider.Advance(TimeSpan.FromHours(1));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(0);

        tcs.SetResult();
        await Task.Yield();
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task ScheduledJobs_RunMultipleTimes()
    {
        var counter = new AtomicCounter();

        await using var sut = CreateService([
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
    public async Task ScheduledJobs_CanBeDynamicallyEnabled()
    {
        var counter = new AtomicCounter();
        var enabled = new AtomicBool();

        await using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), counter, enabled: (_, _) => 
            {
                return ValueTask.FromResult(JobShouldRunResult.Conditional(nameof(enabled), enabled.Value));
            }),
        ]);

        await Start(sut);
        TimeProvider.Advance(TimeSpan.FromHours(1));
        await sut.WaitForRunningScheduledJobs();

        counter.Value.Should().Be(0);

        enabled.Set(true);
        for (var i = 0; i < 9; i++)
        {
            // advance forward 9 hours in 1 hour increments to trigger interim timers
            TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        await sut.WaitForRunningScheduledJobs();

        counter.Value.Should().Be(1);

        await Stop(sut);
    }

    [Fact]
    public async Task ScheduledJobs_Interval_IsBetweenRuns()
    {
        var counter = new AtomicCounter();

        await using var sut = CreateService([
            Registration.Scheduled(
                TimeSpan.FromHours(1),
                services =>
                {
                    var timeProvider = services.GetRequiredService<FakeTimeProvider>();

                    counter.Increment();
                    
                    // this job took 10 minutes
                    timeProvider.Advance(TimeSpan.FromMinutes(10));
                    
                    return Task.CompletedTask;
                },
                services => ValueTask.FromResult(true)),
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

        await using var sut = CreateService([
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

        await using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), "test", counter),
        ]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task ScheduledJobs_WithLease_CanBeDisabled()
    {
        var counter = new AtomicCounter();
        var enabled = new AtomicBool();

        var leaseName = "test";
        var initialLeaseInfo = await GetLeaseInfo(leaseName);

        await using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), "test", counter, enabled: (_, _) =>
            {
                return ValueTask.FromResult(JobShouldRunResult.Conditional(nameof(enabled), enabled.Value));
            }),
        ]);

        await Start(sut);
        TimeProvider.Advance(TimeSpan.FromHours(1));
        await sut.WaitForRunningScheduledJobs();

        var postLeaseInfo = await GetLeaseInfo(leaseName);
        counter.Value.Should().Be(0);
        postLeaseInfo.LastAcquiredAt.Should().Be(initialLeaseInfo.LastAcquiredAt);

        enabled.Set(true);
        for (var i = 0; i < 9; i++)
        {
            // advance forward 9 hours in 1 hour increments to trigger interim timers
            TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        await sut.WaitForRunningScheduledJobs();
        postLeaseInfo = await GetLeaseInfo(leaseName);

        counter.Value.Should().Be(1);
        postLeaseInfo.LastAcquiredAt.Should().NotBe(initialLeaseInfo.LastAcquiredAt);

        await Stop(sut);
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

        await using var sut = CreateService([
            Counter.Scheduled(TimeSpan.FromHours(1), "test", counter),
        ]);

        await Start(sut);
        counter.Value.Should().Be(0);

        // advance 20 minutes, 1 hour since the lease was released (+ some seconds due to random jitter)
        TimeProvider.Advance(TimeSpan.FromMinutes(21));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(1);
        var leaseInfo = await GetLeaseInfo("test");
        leaseInfo.LastReleasedAt.Should().Be(TimeProvider.GetUtcNow());

        // advance 40 more minutes and renew the lease
        TimeProvider.Advance(TimeSpan.FromMinutes(40));
        result = await Provider.TryAcquireLease("test", TimeSpan.FromMinutes(1));
        Assert.True(result.IsLeaseAcquired);
        await Provider.ReleaseLease(result.Lease);

        // advance 20 more minutes, 1 hour since the job last completed (+ some seconds due to random jitter)
        TimeProvider.Advance(TimeSpan.FromMinutes(21));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(1);

        // advance 40 more minutes, 1 hour since the lease was last released (+ some seconds due to random jitter)
        TimeProvider.Advance(TimeSpan.FromMinutes(40));
        await sut.WaitForRunningScheduledJobs();
        counter.Value.Should().Be(2);
        leaseInfo = await GetLeaseInfo("test");
        leaseInfo.LastReleasedAt.Should().Be(TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task MultipleConditions_AllTrue_Runs()
    {
        var conditions = Enumerable.Range(0, 10).Select(_ => new ConstCondition(true, tags: []));
        var counter = new AtomicCounter();

        await using var sut = CreateService(
            [Counter.RunAt(JobHostLifecycles.Start, counter)],
            [.. conditions]);

        await Run(sut);

        counter.Value.Should().Be(1);
    }

    [Fact]
    public async Task MultipleConditions_SomeFalse_DoesNotRun()
    {
        var conditions = Enumerable.Range(0, 10).Select(_ => new ConstCondition(true, tags: []));
        var counter = new AtomicCounter();

        await using var sut = CreateService(
            [Counter.RunAt(JobHostLifecycles.Start, counter)],
            [.. conditions, new ConstCondition(false, tags: [])]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task Conditions_CanTarget_AllJobs()
    {
        var condition = new ConstCondition(false, tags: []);
        var counter = new AtomicCounter();

        await using var sut = CreateService(
            [Counter.RunAt(JobHostLifecycles.Start, counter, tags: ["foo"]), Counter.RunAt(JobHostLifecycles.Start, counter, tags: ["bar"]), Counter.RunAt(JobHostLifecycles.Start, counter)],
            [condition]);

        await Run(sut);

        counter.Value.Should().Be(0);
    }

    [Fact]
    public async Task Conditions_CanTarget_TaggedJobs()
    {
        var condition = new ConstCondition(false, tags: ["foo"]);
        var fooCounter = new AtomicCounter();
        var barCounter = new AtomicCounter();
        var noTag = new AtomicCounter();

        await using var sut = CreateService(
            [Counter.RunAt(JobHostLifecycles.Start, fooCounter, tags: ["foo"]), Counter.RunAt(JobHostLifecycles.Start, barCounter, tags: ["bar"]), Counter.RunAt(JobHostLifecycles.Start, noTag)],
            [condition]);

        await Run(sut);

        fooCounter.Value.Should().Be(0);
        barCounter.Value.Should().Be(1);
        noTag.Value.Should().Be(1);
    }

    [Fact]
    public async Task CanBeDisposed_MultipleTimes()
    {
        await using var sut = CreateService([]);

        await Run(sut);
        await sut.DisposeAsync();
        await sut.DisposeAsync(); // should not throw
    }

    [Fact]
    public async Task CanBeDisposed_MultipleTimes_InParallel()
    {
        const int THREAD_COUNT = 10;

        await using var sut = CreateService([]);

        await Run(sut);

        var rst = new ManualResetEventSlim();
        var threads = Enumerable.Range(0, THREAD_COUNT).Select(_ =>
        {
            var thread = new Thread(async () =>
            {
                rst.Wait();
                await sut.DisposeAsync();
            });

            thread.Start();
            return thread;
        }).ToList();

        // wait for all the threads to be ready
        await Task.Delay(10);
        rst.Set();

        foreach (var thread in threads)
        {
            thread.Join();
        }
    }

    private record LeaseInfo(string LeaseName, Guid? Token, DateTimeOffset Expires, DateTimeOffset? LastAcquiredAt, DateTimeOffset? LastReleasedAt);

    private async Task<LeaseInfo> GetLeaseInfo(string leaseName)
    {
        await using var uow = await GetRequiredService<IUnitOfWorkManager>().CreateAsync();
        var conn = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = conn.CreateCommand(/*strpsql*/"SELECT id, token, expires, acquired, released FROM register.lease WHERE id = @id");

        cmd.Parameters.Add<string>("id", NpgsqlTypes.NpgsqlDbType.Text).TypedValue = leaseName;
        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow);
        if (!await reader.ReadAsync())
        {
            return new(leaseName, null, DateTimeOffset.MinValue, null, null);
        }

        var dbLeaseName = await reader.GetFieldValueAsync<string>("id");
        var dbToken = await reader.GetFieldValueAsync<Guid>("token");
        var dbExpires = await reader.GetFieldValueAsync<DateTimeOffset>("expires");
        var dbLastAcquiredAt = await reader.GetFieldValueAsync<DateTimeOffset?>("acquired");
        var dbLastReleasedAt = await reader.GetFieldValueAsync<DateTimeOffset?>("released");

        return new(dbLeaseName, dbToken, dbExpires, dbLastAcquiredAt, dbLastReleasedAt);
    }

    private RecurringJobHostedService CreateService(IEnumerable<JobRegistration> registrations, IEnumerable<IJobCondition>? conditions = null)
        => _factory(Services, [registrations, conditions ?? []]);

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

    private sealed class DelegateJob(
        Func<IServiceProvider, Task> run,
        Func<IServiceProvider, ValueTask<bool>> shouldRun,
        IServiceProvider services)
        : Job
    {
        protected override Task RunAsync(CancellationToken cancellationToken)
            => run(services);

        protected override ValueTask<bool> ShouldRun(CancellationToken cancellationToken)
            => shouldRun(services);
    }

    private sealed class Registration(
        Func<IServiceProvider, IJob> job,
        string name,
        string? leaseName,
        TimeSpan interval,
        JobHostLifecycles runAt,
        IEnumerable<string> tags,
        Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled,
        Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady)
        : JobRegistration(name, leaseName, interval, runAt, tags, enabled, waitForReady)
    {
        public static JobRegistration Create(
            Func<IServiceProvider, IJob> job,
            string? leaseName,
            TimeSpan interval,
            JobHostLifecycles runAt,
            IEnumerable<string> tags,
            Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled,
            Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady)
            => new Registration(job, "test", leaseName, interval, runAt, tags, enabled, waitForReady);

        public static JobRegistration Create(
            Func<IServiceProvider, Task> job,
            Func<IServiceProvider, ValueTask<bool>> shouldRun,
            string? leaseName,
            TimeSpan interval,
            JobHostLifecycles runAt,
            IEnumerable<string> tags,
            Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled,
            Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady)
            => new Registration(services => new DelegateJob(job, shouldRun, services), "test", leaseName, interval, runAt, tags, enabled, waitForReady);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, Func<IServiceProvider, IJob> job)
            => Create(job, null, TimeSpan.Zero, runAt, [], null, null);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, Func<IServiceProvider, Task> job, Func<IServiceProvider, ValueTask<bool>> shouldRun)
            => Create(job, shouldRun, null, TimeSpan.Zero, runAt, [], null, null);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, string leaseName, Func<IServiceProvider, IJob> job)
            => Create(job, leaseName, TimeSpan.Zero, runAt, [], null, null);

        public static new JobRegistration RunAt(JobHostLifecycles runAt, string leaseName, Func<IServiceProvider, Task> job, Func<IServiceProvider, ValueTask<bool>> shouldRun)
            => Create(job, shouldRun, leaseName, TimeSpan.Zero, runAt, [], null, null);

        public static JobRegistration Scheduled(TimeSpan interval, Func<IServiceProvider, Task> job, Func<IServiceProvider, ValueTask<bool>> shouldRun)
            => Create(job, shouldRun, null, interval, JobHostLifecycles.None, [], null, null);

        public override IJob Create(IServiceProvider services)
            => job(services);
    }

    private static class Counter
    {
        public static JobRegistration Create(
            AtomicCounter counter,
            AtomicBool shouldRun,
            string? leaseName,
            TimeSpan interval,
            JobHostLifecycles runAt,
            IEnumerable<string>? tags = null,
            Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled = null,
            Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady = null)
        {
            var shouldRunFn = (IServiceProvider services) =>
            {
                return ValueTask.FromResult(shouldRun.Value);
            };

            var job = (IServiceProvider services) =>
            {
                var timeProvider = services.GetRequiredService<FakeTimeProvider>();

                counter.Increment();
                timeProvider.Advance(TimeSpan.FromSeconds(10));

                return Task.CompletedTask;
            };

            return Registration.Create(job, shouldRunFn, leaseName, interval, runAt, tags ?? [], enabled, waitForReady);
        }

        public static JobRegistration RunAt(
            JobHostLifecycles runAt,
            AtomicCounter counter,
            IEnumerable<string>? tags = null,
            Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled = null,
            Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady = null)
            => Create(counter, new AtomicBool(true), null, TimeSpan.Zero, runAt, tags, enabled, waitForReady);

        public static JobRegistration RunAt(
            JobHostLifecycles runAt,
            string? leaseName,
            AtomicCounter counter,
            IEnumerable<string>? tags = null,
            Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled = null,
            Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady = null)
            => Create(counter, new AtomicBool(true), leaseName, TimeSpan.Zero, runAt, tags, enabled, waitForReady);

        public static JobRegistration RunAt(JobHostLifecycles runAt, AtomicCounter counter, Task waitForReady)
            => Create(counter, new AtomicBool(true), null, TimeSpan.Zero, runAt, null, null, (_, _) => new(waitForReady));

        public static JobRegistration RunAt(JobHostLifecycles runAt, string leaseName, AtomicCounter counter)
            => Create(counter, new AtomicBool(true), leaseName, TimeSpan.Zero, runAt);

        public static JobRegistration Scheduled(
            TimeSpan interval,
            AtomicCounter counter,
            IEnumerable<string>? tags = null,
            Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled = null,
            Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady = null)
            => Create(counter, new AtomicBool(true), null, interval, JobHostLifecycles.None, tags, enabled, waitForReady);

        public static JobRegistration Scheduled(
            TimeSpan interval,
            string? leaseName,
            AtomicCounter counter,
            IEnumerable<string>? tags = null,
            Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>>? enabled = null,
            Func<IServiceProvider, CancellationToken, ValueTask>? waitForReady = null)
            => Create(counter, new AtomicBool(true), leaseName, interval, JobHostLifecycles.None, tags, enabled, waitForReady);

        public static JobRegistration Scheduled(TimeSpan interval, AtomicCounter counter, Task waitForReady)
            => Create(counter, new AtomicBool(true), null, interval, JobHostLifecycles.None, null, null, (_, _) => new(waitForReady));

        public static JobRegistration Scheduled(TimeSpan interval, string leaseName, AtomicCounter counter)
            => Create(counter, new AtomicBool(true), leaseName, interval, JobHostLifecycles.None);
    }

    private sealed class ConstCondition(bool value, IEnumerable<string> tags)
        : IJobCondition
    {
        public string Name => $"const {value}";

        public ImmutableArray<string> JobTags { get; } = [.. tags];

        public ValueTask<bool> ShouldRun(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(value);
    }

    private sealed class DisposableJob(AtomicCounter disposeCounter)
        : Job
        , IDisposable
    {
        public void Dispose()
        {
            disposeCounter.Increment();
        }

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncDisposableJob(AtomicCounter disposeCounter)
        : Job
        , IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            disposeCounter.Increment();

            return ValueTask.CompletedTask;
        }

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BothDisposableJob(AtomicCounter disposeCounter, AtomicCounter asyncDisposeCounter)
        : Job
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

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
