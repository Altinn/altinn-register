﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Register.Extensions;
using Altinn.Register.Tests.Utils;
using Xunit;

namespace Altinn.Register.Tests.UnitTests;

public class AsyncEnumerableExtensionsTests 
{
    [Fact]
    public void DistinctBy_Throws_IfNullSource()
    {
        IAsyncEnumerable<string> enumerable = null!;

        Assert.Throws<ArgumentNullException>(() => enumerable.DistinctBy(i => i.Length));
    }

    [Fact]
    public void DistinctBy_Throws_IfNullSelector()
    {
        IAsyncEnumerable<string> enumerable = new AsyncList<string>();

        Assert.Throws<ArgumentNullException>(() => enumerable.DistinctBy((Func<string, int>)null!));
    }

    [Fact]
    public async Task DistinctBy_Returns_DistinctValues()
    {
        IAsyncEnumerable<string> enumerable = new AsyncList<string> { "a", "b", "c" };

        var result = await enumerable.DistinctBy(i => i.Length).ToListAsync();
        Assert.Single(result);
    }

    [Fact]
    public async Task DistinctBy_Returns_DistinctValues_WithComparer()
    {
        IAsyncEnumerable<string> enumerable = new AsyncList<string> { " a ", "a", " A", "A " };

        var result = await enumerable.DistinctBy(i => i.Trim(), StringComparer.OrdinalIgnoreCase).ToListAsync();
        Assert.Single(result);
    }

    [Fact]
    public void Merge_Throws_IfNullSource()
    {
        IAsyncEnumerable<string> enumerable = null!;

        Assert.Throws<ArgumentNullException>(() => enumerable.Merge(AsyncEnumerable.Empty<string>()));
    }

    [Fact]
    public void Merge_Throws_IfNullSources()
    {
        IAsyncEnumerable<string> enumerable = new AsyncList<string>();

        Assert.Throws<ArgumentNullException>(() => enumerable.Merge([null!]));
    }

    [Fact]
    public async Task Merge_Merges_Sources()
    {
        IAsyncEnumerable<string> enumerable = new AsyncList<string> { "a", "b", "c" };

        var result = await enumerable
            .Merge(new AsyncList<string> { "d", "e", "f" })
            .Merge([new AsyncList<string> { "g", "h", "i" }, new AsyncList<string> { "j", "k", "l" }])
            .ToListAsync();

        Assert.Equal(12, result.Count);
    }

    [Fact]
    public async Task Merge_Propagates_Cancellation()
    {
        using var cts = new CancellationTokenSource();

        var infiniteSequence = Enumerable.Range(0, int.MaxValue).ToAsyncEnumerable();
        var cancelableSequence = new CancellableEnumerable<int>(cts.Token);
        var merged = infiniteSequence.Merge(cancelableSequence);

        await using var enumerator = merged.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(0, enumerator.Current);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, enumerator.Current);

        using var resetEvent = new ManualResetEvent(false);
        cts.Token.Register(() => resetEvent.Set());

        cts.Cancel();
        var ex = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            resetEvent.WaitOne();

            var remaining = 10;
            while (await enumerator.MoveNextAsync())
            {
                await Task.Yield();

                if (remaining-- == 0)
                {
                    throw new InvalidOperationException("Should have cancelled");
                }
            }
        });

        Assert.Equal(cts.Token, ex.CancellationToken);
    }

    [Fact]
    public async Task Merge_Empty()
    {
        var seq = AsyncEnumerableExtensions.Merge<string>([]);
        var result = await seq.ToListAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task Merge_Single()
    {
        var seq = AsyncEnumerableExtensions.Merge([AsyncEnumerable.Range(0, 10)]);
        var result = await seq.ToListAsync();
        Assert.Equal(10, result.Count);
    }

    private sealed class CancellableEnumerable<T> 
        : IAsyncEnumerable<T>
    {
        private readonly CancellationToken _token;

        public CancellableEnumerable(CancellationToken token)
        {
            _token = token;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new Enumerator(_token);
        }

        private class Enumerator : IAsyncEnumerator<T>
        {
            private readonly TaskCompletionSource<bool> _tcs;

            public Enumerator(CancellationToken ct)
            {
                _tcs = new();
                if (ct.IsCancellationRequested)
                {
                    _tcs.TrySetCanceled(ct);
                }
                else
                {
                    ct.Register(() => _tcs.SetCanceled(ct), useSynchronizationContext: false);
                }
            }

            public T Current => throw new InvalidOperationException();

            public ValueTask DisposeAsync()
            {
                _tcs.TrySetCanceled();

                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                return new(_tcs.Task);
            }
        }
    }
}
