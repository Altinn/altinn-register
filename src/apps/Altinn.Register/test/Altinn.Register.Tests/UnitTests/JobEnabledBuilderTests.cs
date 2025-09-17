#nullable enable

using Altinn.Authorization.ServiceDefaults.Jobs;
using Altinn.Register.Tests.Utils;

namespace Altinn.Register.Tests.UnitTests;

public class JobEnabledBuilderTests
{
    [Fact]
    public async Task AllSync()
    {
        var counter = new AtomicCounter();
        var check = JobEnabledBuilder.Default
            .WithCheck(CreateImmediateCheck(counter))
            .WithCheck(CreateImmediateCheck(counter))
            .WithCheck(CreateImmediateCheck(counter))
            .WithCheck(CreateImmediateCheck(counter))
            .WithCheck(CreateImmediateCheck(counter))
            .ToFunc();

        var result = await check(null!, default);
        result.ShouldRun.Should().BeTrue();
        counter.Value.Should().Be(5);
    }

    [Fact]
    public async Task AllAsync()
    {
        var counter = new AtomicCounter();
        var check = JobEnabledBuilder.Default
            .WithCheck(CreateDeferredCheck(counter))
            .WithCheck(CreateDeferredCheck(counter))
            .WithCheck(CreateDeferredCheck(counter))
            .WithCheck(CreateDeferredCheck(counter))
            .WithCheck(CreateDeferredCheck(counter))
            .ToFunc();

        var result = await check(null!, default);
        result.ShouldRun.Should().BeTrue();
        counter.Value.Should().Be(5);
    }

    [Fact]
    public async Task MiddleAsync()
    {
        var counter = new AtomicCounter();
        var check = JobEnabledBuilder.Default
            .WithCheck(CreateImmediateCheck(counter))
            .WithCheck(CreateImmediateCheck(counter))
            .WithCheck(CreateDeferredCheck(counter))
            .WithCheck(CreateImmediateCheck(counter))
            .WithCheck(CreateImmediateCheck(counter))
            .ToFunc();

        var result = await check(null!, default);
        result.ShouldRun.Should().BeTrue();
        counter.Value.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AllSync_WithFalse(int index)
    {
        var counter = new AtomicCounter();
        var builder = JobEnabledBuilder.Default;
        for (var i = 0; i < 5; i++)
        {
            builder = builder.WithCheck(CreateImmediateCheck(counter, i != index));
        }

        var check = builder.ToFunc();

        var result = await check(null!, default);
        result.ShouldRun.Should().BeFalse();
        counter.Value.Should().Be((uint)(index + 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AllAsync_WithFalse(int index)
    {
        var counter = new AtomicCounter();
        var builder = JobEnabledBuilder.Default;
        for (var i = 0; i < 5; i++)
        {
            builder = builder.WithCheck(CreateDeferredCheck(counter, i != index, reason: i.ToString()));
        }

        var check = builder.ToFunc();

        var result = await check(null!, default);
        result.ShouldRun.Should().BeFalse();
        result.Reason.Should().Be(index.ToString());
        counter.Value.Should().Be((uint)(index + 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task OneAsync_WithFalse(int index)
    {
        var counter = new AtomicCounter();
        var builder = JobEnabledBuilder.Default;
        for (var i = 0; i < 5; i++)
        {
            if (i == index)
            {
                builder = builder.WithCheck(CreateDeferredCheck(counter, false, reason: i.ToString()));
            }
            else
            {
                builder = builder.WithCheck(CreateImmediateCheck(counter, reason: i.ToString()));
            }
        }

        var check = builder.ToFunc();

        var result = await check(null!, default);
        result.ShouldRun.Should().BeFalse();
        result.Reason.Should().Be(index.ToString());
        counter.Value.Should().Be((uint)(index + 1));
    }

    private static Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>> CreateImmediateCheck(AtomicCounter counter, bool result = true, string reason = "test")
        => (_, _) =>
        {
            counter.Increment();
            return ValueTask.FromResult(JobShouldRunResult.Conditional(reason, result));
        };

    private static Func<IServiceProvider, CancellationToken, ValueTask<JobShouldRunResult>> CreateDeferredCheck(AtomicCounter counter, bool result = true, string reason = "test")
        => async (_, _) =>
        {
            await Task.Yield();
            counter.Increment();
            return JobShouldRunResult.Conditional(reason, result);
        };
}
