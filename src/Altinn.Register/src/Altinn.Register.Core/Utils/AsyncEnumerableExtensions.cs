#nullable enable

using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Utils;

/// <summary>
/// Extension methods for <see cref="IAsyncEnumerable{T}"/>
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Wraps exceptions thrown by the source enumerable in a new exception.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="wrap">The exception wrapper.</param>
    /// <param name="cancellationToken">Cancellation token (used to filter out <see cref="OperationCanceledException"/>s).</param>
    /// <returns>The source enumerable, but with any exceptions optionally wrapped by <paramref name="wrap"/>.</returns>
    public static IAsyncEnumerable<T> WrapExceptions<T>(
        this IAsyncEnumerable<T> source,
        Func<Exception, Exception?> wrap,
        CancellationToken cancellationToken)
    {
        Guard.IsNotNull(source);
        Guard.IsNotNull(wrap);

        return new WrapExceptionsAsyncEnumerable<T>(source, wrap, cancellationToken);
    }

    private sealed class WrapExceptionsAsyncEnumerable<T>
        : IAsyncEnumerable<T>
    {
        private readonly IAsyncEnumerable<T> _source;
        private readonly Func<Exception, Exception?> _wrap;
        private readonly CancellationToken _cancellationToken;

        public WrapExceptionsAsyncEnumerable(
            IAsyncEnumerable<T> source,
            Func<Exception, Exception?> wrap,
            CancellationToken cancellationToken)
        {
            _source = source;
            _wrap = wrap;
            _cancellationToken = cancellationToken;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            try
            {
                return new Enumerator(_source.GetAsyncEnumerator(cancellationToken), _wrap, _cancellationToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_wrap(ex) is Exception wrapped)
                {
                    throw wrapped;
                }

                throw;
            }
        }

        private sealed class Enumerator
            : IAsyncEnumerator<T>
        {
            private readonly IAsyncEnumerator<T> _source;
            private readonly Func<Exception, Exception?> _wrap;
            private readonly CancellationToken _cancellationToken;

            public Enumerator(
                IAsyncEnumerator<T> source,
                Func<Exception, Exception?> wrap,
                CancellationToken cancellationToken)
            {
                _source = source;
                _wrap = wrap;
                _cancellationToken = cancellationToken;
            }

            public T Current => _source.Current;

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await _source.DisposeAsync();
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (_wrap(ex) is Exception wrapped)
                    {
                        throw wrapped;
                    }

                    throw;
                }
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    return await _source.MoveNextAsync();
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (_wrap(ex) is Exception wrapped)
                    {
                        throw wrapped;
                    }

                    throw;
                }
            }
        }
    }
}
