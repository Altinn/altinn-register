#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Altinn.Register.Extensions;

/// <summary>
/// Extension methods for <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Merges the specified <see cref="IAsyncEnumerable{T}"/> instances into a single <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements of the <paramref name="sources"/>.</typeparam>
    /// <param name="sources">The <see cref="IAsyncEnumerable{T}"/>s to merge.</param>
    /// <returns>A merged <see cref="IAsyncEnumerable{T}"/>.</returns>
    public static IAsyncEnumerable<T> Merge<T>(ReadOnlySpan<IAsyncEnumerable<T>> sources)
        => sources switch
        {
            [] => AsyncEnumerable.Empty<T>(),
            [var first] => first,
            [MergedAsyncEnumerable<T> first, .. var rest] => first.MergeWith(rest),
            _ => MergedAsyncEnumerable<T>.Create(sources),
        };

    /// <summary>
    /// Merges the specified <see cref="IAsyncEnumerable{T}"/> instances into a single <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequences.</typeparam>
    /// <param name="self">The first enumerable.</param>
    /// <param name="rest">The remaining enumerables.</param>
    /// <returns>The merged sequence.</returns>
    public static IAsyncEnumerable<T> Merge<T>(this IAsyncEnumerable<T> self, ReadOnlySpan<IAsyncEnumerable<T>> rest)
        => Merge([self, .. rest]);

    /// <summary>
    /// Merges the specified <see cref="IAsyncEnumerable{T}"/> instances into a single <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequences.</typeparam>
    /// <param name="self">The first enumerable.</param>
    /// <param name="other">The second enumerable.</param>
    /// <returns>The merged sequence.</returns>
    public static IAsyncEnumerable<T> Merge<T>(this IAsyncEnumerable<T> self, IAsyncEnumerable<T> other)
        => Merge([self, other]);

    /// <summary>
    /// Writes the elements of the <see cref="IAsyncEnumerable{T}"/> to the specified <see cref="ChannelWriter{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the <paramref name="source"/>.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="writer">The channel writer.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public static async Task WriteToAsync<T>(this IAsyncEnumerable<T> source, ChannelWriter<T> writer, CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            if (!await writer.WaitToWriteAsync(cancellationToken))
            {
                // Can't write more items, the channel is closed.
                return;
            }

            await writer.WriteAsync(item, cancellationToken);
        }
    }

    private sealed class MergedAsyncEnumerable<T>
        : IAsyncEnumerable<T>
    {
        private readonly ImmutableArray<IAsyncEnumerable<T>> _sources;

        private MergedAsyncEnumerable(ImmutableArray<IAsyncEnumerable<T>> sources)
        {
            _sources = sources;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new Enumerator(_sources, cancellationToken);

        public MergedAsyncEnumerable<T> MergeWith(ReadOnlySpan<IAsyncEnumerable<T>> sources)
        {
            return MergedAsyncEnumerable<T>.Create([.. _sources, .. sources]);
        }

        public static MergedAsyncEnumerable<T> Create(ReadOnlySpan<IAsyncEnumerable<T>> sources)
        {
            var length = sources.Length;
            foreach (var source in sources)
            {
                if (source is null)
                {
                    throw new ArgumentNullException(nameof(sources));
                }

                if (source is MergedAsyncEnumerable<T> merged)
                {
                    length += merged._sources.Length - 1;
                }
            }

            var builder = ImmutableArray.CreateBuilder<IAsyncEnumerable<T>>(length);
            foreach (var source in sources)
            {
                if (source is MergedAsyncEnumerable<T> merged)
                {
                    builder.AddRange(merged._sources);
                }
                else
                {
                    builder.Add(source);
                }
            }

            return new MergedAsyncEnumerable<T>(builder.DrainToImmutable());
        }

        private sealed class Enumerator
            : IAsyncEnumerator<T>
            , IValueTaskSource<bool>
        {
            private readonly Lock _lock = new();
            private readonly CancellationToken _cancellationToken;
            private readonly SourceState[] _sources;
            private ManualResetValueTaskSourceCore<bool> _tcs; // mutable struct - must not be readonly
            private bool _waiting = false;
            private int _index;
            private T _current = default!;

            public Enumerator(ImmutableArray<IAsyncEnumerable<T>> sources, CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
                _tcs = default;
                _tcs.RunContinuationsAsynchronously = true;
                _sources = new SourceState[sources.Length];
                _index = 0;

                for (var i = 0; i < sources.Length; i++)
                {
                    _sources[i] = new SourceState
                    {
                        Source = sources[i].GetAsyncEnumerator(cancellationToken),
                        Status = SourceState.STATUS_INITIAL,
                        Callback = null,
                    };
                }
            }

            T IAsyncEnumerator<T>.Current
                => _current;

            async ValueTask IAsyncDisposable.DisposeAsync()
            {
                for (var i = 0; i < _sources.Length; i++)
                {
                    ref var source = ref _sources[i];
                    await source.Source.DisposeAsync();
                }
            }

            ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
            {
                _cancellationToken.ThrowIfCancellationRequested();

                lock (_lock)
                {
                    Debug.Assert(!_waiting);
                    bool anyPending = false;

                    // We need to not start at 0 every time, such that if all sources are always ready, we don't always pick the same one.
                    for (var offset = 0; offset < _sources.Length; offset++)
                    {
                        // the index
                        var i = (_index + offset) % _sources.Length;
                        ref var source = ref _sources[i];

                        switch (source.Status)
                        {
                            case SourceState.STATUS_INITIAL:
                                // source is in an initial state, meaning we're not pending, and have no current value.
                                var awaiter = source.Source.MoveNextAsync().ConfigureAwait(false).GetAwaiter();
                                if (awaiter.IsCompleted)
                                {
                                    // we already have a value, complete synchronously
                                    if (awaiter.GetResult())
                                    {
                                        _current = source.Source.Current;
                                        _index = (i + 1) % _sources.Length;
                                        source.Status = SourceState.STATUS_INITIAL;
                                        return new ValueTask<bool>(true);
                                    }
                                    else
                                    {
                                        source.Status = SourceState.STATUS_DONE;
                                    }
                                }
                                else
                                {
                                    // we need to wait for the value to be ready
                                    source.Status = SourceState.STATUS_PENDING;
                                    source.Awaiter = awaiter;
                                    var callback = source.Callback ??= CreateCallback(this, i);
                                    awaiter.UnsafeOnCompleted(callback);
                                    anyPending = true;
                                }

                                break;

                            case SourceState.STATUS_PENDING:
                                // we're already waiting for a value, nothing to do here.
                                anyPending = true;
                                break;

                            case SourceState.STATUS_READY:
                                // an async MoveNextAsync has completed, we can now read the value.
                                Debug.Assert(source.Awaiter.IsCompleted);
                                if (source.Awaiter.GetResult())
                                {
                                    _current = source.Source.Current;
                                    _index = (i + 1) % _sources.Length;
                                    source.Status = SourceState.STATUS_INITIAL;
                                    return new ValueTask<bool>(true);
                                }
                                else
                                {
                                    source.Status = SourceState.STATUS_DONE;
                                }

                                break;

                            case SourceState.STATUS_DONE:
                                // we're done with this source, move on to the next one.
                                break;
                        }
                    }

                    // if we've reached this point, either all sources are done, or we're waiting for a value.
                    if (anyPending)
                    {
                        // we're waiting for a value, return a task that will complete when any of the sources are ready.
                        _tcs.Reset();
                        _waiting = true;
                        return new ValueTask<bool>(this, _tcs.Version);
                    }
                }

                // no remaining sources, we're done.
                return new ValueTask<bool>(false);
            }

            bool IValueTaskSource<bool>.GetResult(short token)
            {
                lock (_lock)
                {
                    return _tcs.GetResult(token);
                }
            }

            ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
            {
                lock (_lock)
                {
                    return _tcs.GetStatus(token);
                }
            }

            void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                lock (_lock)
                {
                    _tcs.OnCompleted(continuation, state, token, flags);
                }
            }

            private void OnSourceReady(int index)
            {
                lock (_lock)
                {
                    ref var source = ref _sources[index];
                    Debug.Assert(source.Status == SourceState.STATUS_PENDING);
                    source.Status = SourceState.STATUS_READY;

                    if (!_waiting)
                    {
                        // we're not currently in a waiting state, meaning MoveNextAsync is likely to be called again soon, and it will deal with notifications.
                        return;
                    }

                    bool moveNextResult;
                    try
                    {
                        moveNextResult = source.Awaiter.GetResult();
                        source.Status = SourceState.STATUS_INITIAL;
                    }
                    catch (Exception e)
                    {
                        _index = (index + 1) % _sources.Length;
                        _waiting = false;
                        _tcs.SetException(e);
                        return;
                    }

                    if (moveNextResult)
                    {
                        _current = source.Source.Current;
                        _index = (index + 1) % _sources.Length;
                        _waiting = false;
                        _tcs.SetResult(true);
                        return;
                    }

                    source.Status = SourceState.STATUS_DONE;
                    var allDone = _sources.All(static s => s.Status == SourceState.STATUS_DONE);

                    if (allDone)
                    {
                        _index = (index + 1) % _sources.Length;
                        _waiting = false;
                        _tcs.SetResult(false);
                    }
                }
            }

            private static Action CreateCallback(Enumerator enumerator, int index)
            {
                return () => enumerator.OnSourceReady(index);
            }

            private struct SourceState
            {
                public const byte STATUS_INITIAL = 0;
                public const byte STATUS_PENDING = 1;
                public const byte STATUS_READY = 2;
                public const byte STATUS_DONE = 3;

                public IAsyncEnumerator<T> Source;
                public byte Status;
                public Action? Callback;
                public ConfiguredValueTaskAwaitable<bool>.ConfiguredValueTaskAwaiter Awaiter;
            }
        }
    }
}
