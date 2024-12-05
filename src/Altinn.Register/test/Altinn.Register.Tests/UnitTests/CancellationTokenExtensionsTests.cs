#nullable enable

using Altinn.Register.Core.Utils;

namespace Altinn.Register.Tests.UnitTests;

public class CancellationTokenExtensionsTests
{
    [Fact]
    public async Task WaitForCancellationAsync_NormalAsyncFlow()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var waiter = token.WaitForCancellationAsync();
        var awaiter = waiter.GetAwaiter();

        awaiter.IsCompleted.Should().BeFalse();

        var callbacked = false;
        awaiter.UnsafeOnCompleted(() => callbacked = true);

        callbacked.Should().BeFalse();
        await cts.CancelAsync();

        callbacked.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForCancellationAsync_UnsafeOnCompleted_AfterCancellation_InvokesImmediately()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var waiter = token.WaitForCancellationAsync();
        var awaiter = waiter.GetAwaiter();

        awaiter.IsCompleted.Should().BeFalse();

        await cts.CancelAsync();
        var callbacked = false;
        awaiter.UnsafeOnCompleted(() => callbacked = true);
        callbacked.Should().BeTrue();
    }

    [Fact]

    public async Task WaitForCancellationAsync_Await()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var task = Task.Run(async () => await token.WaitForCancellationAsync());
        await task.WaitAsync(TimeSpan.FromMilliseconds(10)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        task.IsCompleted.Should().BeFalse();

        await cts.CancelAsync();
        await task.WaitAsync(TimeSpan.FromMilliseconds(100));
        task.IsCompleted.Should().BeTrue();
    }
}
