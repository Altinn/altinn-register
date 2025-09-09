using MassTransit.Testing;

namespace Altinn.Register.TestUtils.MassTransit;

public abstract class AsyncMessageList<T, TOuter>
    : IAsyncEnumerable<T>
    where TOuter : class, IAsyncListElement
{
    /// <inheritdoc/>
    public abstract IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items already completed (does not wait for new items).
    /// </summary>
    public abstract IAsyncEnumerable<T> Completed { get; }

    /// <summary>
    /// Gets the outer enumerable.
    /// </summary>
    public abstract IAsyncEnumerable<TOuter> Outer { get; }
}

/// <summary>
/// Represents a wrapped <see cref="IAsyncEnumerable{T}"/>, with the possibility of getting at the outer enumerable.
/// </summary>
/// <typeparam name="T">The inner item type.</typeparam>
/// <typeparam name="TOuter">The outer item type.</typeparam>
internal sealed class AsyncMessageList<T, TOuter, TBase>
    : AsyncMessageList<T, TOuter>
    where TOuter : class, IAsyncListElement, TBase
    where TBase : class, IAsyncListElement
{
    private readonly IAsyncElementList<TBase> _source;
    private readonly FilterDelegate<TBase> _filter;
    private readonly Func<TOuter, T> _selector;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncUnwrappedEnumerable{T, TOuter}"/> class.
    /// </summary>
    /// <param name="source">The source <see cref="IAsyncElementList{T}"/>.</param>
    /// <param name="selector">The selector that unwraps <typeparamref name="TOuter"/>s to <typeparamref name="T"/>s.</param>
    public AsyncMessageList(
        IAsyncElementList<TBase> source,
        FilterDelegate<TBase> filter,
        Func<TOuter, T> selector,
        CancellationToken cancellationToken)
    {
        _source = source;
        _filter = filter;
        _selector = selector;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);

        await foreach (TOuter outer in _source.SelectAsync(_filter, cts.Token).OfType<TOuter>())
        {
            yield return _selector(outer);
        }
    }

    /// <inheritdoc/>
    public override IAsyncEnumerable<T> Completed => _source.SelectExisting(_filter).OfType<TOuter>().Select(_selector);

    /// <inheritdoc/>
    public override IAsyncEnumerable<TOuter> Outer => _source.SelectAsync(_filter, _cancellationToken).OfType<TOuter>();
}
