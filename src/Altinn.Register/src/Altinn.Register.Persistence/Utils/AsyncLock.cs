using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Persistence.Utils;

/// <summary>
/// An asynchronous lock.
/// </summary>
/// <remarks>
/// Lock is not reentrant.
/// </remarks>
internal sealed class AsyncLock()
    : AsyncConcurrencyLimiter(1)
{
}

/// <summary>
/// An asynchronous concurrency limiter.
/// </summary>
internal class AsyncConcurrencyLimiter
    : IDisposable
{
    private readonly SemaphoreSlim _semaphoreSlim;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncConcurrencyLimiter" /> class.
    /// </summary>
    /// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
    public AsyncConcurrencyLimiter(int maxConcurrency)
    {
        Guard.IsGreaterThanOrEqualTo(maxConcurrency, 1);

        _semaphoreSlim = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Dispose(disposing: true);
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="AsyncConcurrencyLimiter" />.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if disposing, <see langword="false"/> if called from a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _semaphoreSlim.Dispose();
        }
    }

    /// <summary>
    /// Acquires a lock to access the resource thread-safe.
    /// </summary>
    /// <returns>An <see cref="IDisposable" /> that releases the lock on <see cref="IDisposable.Dispose" />.</returns>
    public async Task<IDisposable> Acquire(CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        return new Ticket(_semaphoreSlim);
    }

    /// <summary>
    /// A lock to synchronize threads.
    /// </summary>
    private sealed class Ticket : IDisposable
    {
        private SemaphoreSlim? _semaphoreSlim;

        /// <summary>
        /// Initializes a new instance of the <see cref="Ticket" /> class.
        /// </summary>
        /// <param name="semaphoreSlim">The semaphore slim to synchronize threads.</param>
        public Ticket(SemaphoreSlim semaphoreSlim)
        {
            _semaphoreSlim = semaphoreSlim;
        }

        ~Ticket()
        {
            if (_semaphoreSlim != null)
            {
                ThrowHelper.ThrowInvalidOperationException("Lock not released.");
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Interlocked.Exchange(ref _semaphoreSlim, null)?.Release();
        }
    }
}
