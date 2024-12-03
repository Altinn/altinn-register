#nullable enable

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Extensions;

/// <summary>
/// Extension methods for <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Split the elements of a sequence into chunks of size at most <paramref name="size"/>.
    /// </summary>
    /// <remarks>
    /// Every chunk except the last will be of size <paramref name="size"/>.
    /// The last chunk will contain the remaining elements and may be of a smaller size.
    /// </remarks>
    /// <param name="source">
    /// An <see cref="IAsyncEnumerable{T}"/> whose elements to chunk.
    /// </param>
    /// <param name="size">
    /// Maximum size of each chunk.
    /// </param>
    /// <typeparam name="TSource">
    /// The type of the elements of source.
    /// </typeparam>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> that contains the elements of the input sequence split into chunks of size <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="size"/> is below 1.
    /// </exception>
    public static IAsyncEnumerable<TSource[]> Chunk<TSource>(
        this IAsyncEnumerable<TSource> source,
        int size)
    {
        Guard.IsNotNull(source);
        Guard.IsGreaterThan(size, 0);

        return EnumerableChunkIterator(source, size);

        // copied directly from Enumerable.Chunk, just made async
        static async IAsyncEnumerable<TSource[]> EnumerableChunkIterator(
            IAsyncEnumerable<TSource> source,
            int size,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await using var e = source.GetAsyncEnumerator(cancellationToken);

            // Before allocating anything, make sure there's at least one element.
            if (await e.MoveNextAsync())
            {
                // Now that we know we have at least one item, allocate an initial storage array. This is not
                // the array we'll yield.  It starts out small in order to avoid significantly overallocating
                // when the source has many fewer elements than the chunk size.
                int arraySize = Math.Min(size, 4);
                int i;
                do
                {
                    var array = new TSource[arraySize];

                    // Store the first item.
                    array[0] = e.Current;
                    i = 1;

                    if (size != array.Length)
                    {
                        // This is the first chunk. As we fill the array, grow it as needed.
                        for (; i < size && await e.MoveNextAsync(); i++)
                        {
                            if (i >= array.Length)
                            {
                                arraySize = (int)Math.Min((uint)size, 2 * (uint)array.Length);
                                Array.Resize(ref array, arraySize);
                            }

                            array[i] = e.Current;
                        }
                    }
                    else
                    {
                        // For all but the first chunk, the array will already be correctly sized.
                        // We can just store into it until either it's full or MoveNext returns false.
                        TSource[] local = array; // avoid bounds checks by using cached local (`array` is lifted to iterator object as a field)
                        Debug.Assert(local.Length == size);
                        for (; (uint)i < (uint)local.Length && await e.MoveNextAsync(); i++)
                        {
                            local[i] = e.Current;
                        }
                    }

                    if (i != array.Length)
                    {
                        Array.Resize(ref array, i);
                    }

                    yield return array;
                }
                while (i >= size && await e.MoveNextAsync());
            }
        }
    }

    /// <summary>
    /// Returns distinct elements from a sequence according to a specified key selector function and using a specified comparer to compare keys.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
    /// <typeparam name="TKey">The type of key to distinguish elements by.</typeparam>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="IEqualityComparer{T}"/> to compare keys.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that contains distinct elements from the source sequence.</returns>
    public static IAsyncEnumerable<TSource> DistinctBy<TSource, TKey>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        var outerComparer = new DistinctByComparer<TSource, TKey>(keySelector, comparer);

        return source.Distinct(outerComparer);
    }

    /// <summary>
    /// Returns distinct elements from a sequence according to a specified key selector function.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
    /// <typeparam name="TKey">The type of key to distinguish elements by.</typeparam>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that contains distinct elements from the source sequence.</returns>
    public static IAsyncEnumerable<TSource> DistinctBy<TSource, TKey>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        => DistinctBy(source, keySelector, comparer: null);

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

    private sealed class DistinctByComparer<TSource, TKey>
        : IEqualityComparer<TSource>
    {
        private readonly Func<TSource, TKey> _keySelector;
        private readonly IEqualityComparer<TKey> _comparer;

        public DistinctByComparer(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer)
        {
            _keySelector = keySelector;
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        public bool Equals(TSource? x, TSource? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return _comparer.Equals(_keySelector(x), _keySelector(y));
        }

        public int GetHashCode([DisallowNull] TSource obj)
        {
            var key = _keySelector(obj);
            if (key is null)
            {
                return 0;
            }

            return _comparer.GetHashCode(key);
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
        {
            var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = true,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

            var writer = channel.Writer;
            var tasks = new Task[_sources.Length];
            for (var i = 0; i < _sources.Length; i++)
            {
                var source = _sources[i];
                tasks[i] = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await source.WriteToAsync(writer, cancellationToken);
                        }
                        catch (Exception e)
                        {
                            writer.TryComplete(e);
                        }
                    },
                    cancellationToken);
            }

            _ = Task.Run(
                async () =>
                {
                    await Task.WhenAll(tasks);
                    writer.TryComplete();
                }, 
                cancellationToken);

            return channel.Reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        }

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
    }
}
