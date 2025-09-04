using MassTransit.Testing;

namespace Altinn.Register.TestUtils.MassTransit;

/// <summary>
/// Represents a wrapped <see cref="IAsyncEnumerable{T}"/>, with the possibility of getting at the outer enumerable.
/// </summary>
/// <typeparam name="T">The inner item type.</typeparam>
/// <typeparam name="TOuter">The outer item type.</typeparam>
public sealed class AsyncUnwrappedEnumerable<T, TOuter>
    : IAsyncEnumerable<T>
{
    private readonly IAsyncEnumerable<TOuter> _source;
    private readonly Func<TOuter, T> _selector;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncUnwrappedEnumerable{T, TOuter}"/> class.
    /// </summary>
    /// <param name="source">The source <see cref="IAsyncEnumerable{T}"/>.</param>
    /// <param name="selector">The selector that unwraps <typeparamref name="TOuter"/>s to <typeparamref name="T"/>s.</param>
    public AsyncUnwrappedEnumerable(IAsyncEnumerable<TOuter> source, Func<TOuter, T> selector)
    {
        _source = source;
        _selector = selector;
    }

    /// <inheritdoc/>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => _source.Select(_selector).GetAsyncEnumerator(cancellationToken);

    /// <summary>
    /// Gets the outer enumerable.
    /// </summary>
    public IAsyncEnumerable<TOuter> Outer => _source;
}
