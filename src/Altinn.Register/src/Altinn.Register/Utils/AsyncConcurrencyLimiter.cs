#nullable enable

using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Utils;

/// <summary>
/// An async-enabled concurrency limiter.
/// </summary>
internal class AsyncConcurrencyLimiter
    : IDisposable
{
    private readonly SemaphoreSlim _semaphoreSlim;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncConcurrencyLimiter" /> class.
    /// </summary>
    /// <param name="maxConcurrency">The maximum number of concurrent operations that can run at once.</param>
    public AsyncConcurrencyLimiter(int maxConcurrency)
    {
        Guard.IsGreaterThanOrEqualTo(maxConcurrency, 1);

        _semaphoreSlim = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _semaphoreSlim.Dispose();
    }

    /// <summary>
    /// Acquires a lock to access the resource thread-safe.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>An <see cref="IDisposable" /> that releases the lock on <see cref="IDisposable.Dispose" />.</returns>
    public async Task<IDisposable> Acquire(CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        return new Ticket(_semaphoreSlim);
    }

    /// <summary>
    /// Attempts to synchronously acquire a ticket.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that releases the lock on <see cref="IDisposable.Dispose" /> if a ticket was acquired, otherwise <see langword="null"/>.</returns>
    public IDisposable? TryAcquire()
    {
        var acquired = _semaphoreSlim.Wait(0);
        if (!acquired)
        {
            return null;
        }

        return new Ticket(_semaphoreSlim);
    }

    /// <summary>
    /// A lock to synchronize threads.
    /// </summary>
    private sealed class Ticket : IDisposable
    {
        private SemaphoreSlim? _semaphoreSlim;
#if DEBUG
        private readonly System.Diagnostics.StackTrace _stackTrace = new();
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="Ticket" /> class.
        /// </summary>
        /// <param name="semaphoreSlim">The semaphore slim to synchronize threads.</param>
        public Ticket(SemaphoreSlim semaphoreSlim)
        {
            _semaphoreSlim = semaphoreSlim;
        }

#if DEBUG
        ~Ticket()
        {
            if (_semaphoreSlim != null)
            {
                ThrowHelper.ThrowInvalidOperationException($"Lock not released. Created at:\n{_stackTrace}");
            }
        }
#endif

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Interlocked.Exchange(ref _semaphoreSlim, null)?.Release();
        }
    }
}
