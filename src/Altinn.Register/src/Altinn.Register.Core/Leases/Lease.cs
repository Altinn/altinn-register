#nullable enable

using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Altinn.Register.Core.Leases;

/// <summary>
/// An owned lease that auto-renews and releases the lease when disposed.
/// </summary>
public sealed partial class Lease
    : IAsyncDisposable
{
    /// <summary>
    /// How often the lease should be renewed.
    /// </summary>
    public static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RenewalSafetyMargin = TimeSpan.FromSeconds(5);

    private static readonly TimerCallback _timerCallback = static (object? state) =>
    {
        Debug.Assert(state is Lease, $"Expected {typeof(Lease)}, got {state}");
        ((Lease)state).Tick();
    };

    private readonly ILeaseProvider _leaseProvider;
    private readonly ILogger<Lease> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _cts;
    private readonly StackTrace? _source;
    private readonly ITimer _timer;

    private int _disposed = 0;
    private LeaseTicket _ticket;

    // For testing purposes
    private Task _tickTask = Task.CompletedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lease"/> class.
    /// </summary>
    public Lease(
        ILeaseProvider leaseProvider,
        ILogger<Lease> logger,
        TimeProvider timeProvider,
        LeaseTicket ticket,
        StackTrace? source,
        CancellationToken outerScopeToken)
    {
        Guard.IsNotNull(leaseProvider);
        Guard.IsNotNull(logger);
        Guard.IsNotNull(timeProvider);
        Guard.IsNotNull(ticket);

        #if DEBUG
        Guard.IsNotNull(source);
        #endif

        _leaseProvider = leaseProvider;
        _logger = logger;
        _timeProvider = timeProvider;
        _ticket = ticket;
        _source = source;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(outerScopeToken);

        // creates the timer in an inert state
        _timer = timeProvider.CreateTimer(
            callback: _timerCallback,
            state: this,
            dueTime: Timeout.InfiniteTimeSpan,
            period: Timeout.InfiniteTimeSpan);

        UpdateTimer();
    }

#if DEBUG
    /// <summary>
    /// Destructor.
    /// </summary>
    ~Lease()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            Log.LeaseNotDisposed(_logger, _ticket.LeaseId, _source);
        }
    }
#endif

    /// <summary>
    /// Gets a <see cref="CancellationToken"/> that is triggered when the lease is disposed or expires.
    /// </summary>
    public CancellationToken Token
        => _cts.Token;

    /// <summary>
    /// Gets the lease id.
    /// </summary>
    public string LeaseId
        => _ticket.LeaseId;

    /// <summary>
    /// Gets the current lease token.
    /// </summary>
    public Guid LeaseToken
        => Volatile.Read(ref _ticket).Token;

    /// <summary>
    /// Gets the task that represents the lease renewal.
    /// Only used for testing purposes.
    /// </summary>
    internal Task TickTask
        => Volatile.Read(ref _tickTask);

    /// <summary>
    /// Gets the current expiry of the lease.
    /// </summary>
    internal DateTimeOffset CurrentExpiry
        => Volatile.Read(ref _ticket).Expires;

    private void Tick()
    {
        if (Volatile.Read(ref _disposed) == 1 
            || Token.IsCancellationRequested)
        {
            return;
        }

        Volatile.Write(ref _tickTask, Task.Run(UpdateLease, Token));

        async Task UpdateLease()
        {
            var result = await _leaseProvider.TryRenewLease(_ticket, LeaseRenewalInterval, Token);

            if (!result.IsLeaseAcquired)
            {
                // failed to renew the lease - it's likely that the lease has been lost
                // either due to time drift or network latency - signal using the cancellation token
                await _cts.CancelAsync();
                return;
            }

            Volatile.Write(ref _ticket, result.Lease);
            UpdateTimer();
        }
    }

    private void UpdateTimer()
    {
        var ticket = Volatile.Read(ref _ticket);
        var expiry = ticket.Expires;
        var now = _timeProvider.GetUtcNow();
        var duration = (expiry - now) - RenewalSafetyMargin;
        
        if (duration <= TimeSpan.Zero)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            Tick();
            return;
        }

        _timer.Change(duration, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
#if DEBUG
        GC.SuppressFinalize(this);
#endif

        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
        {
            // already disposed
            return ValueTask.CompletedTask;
        }

        return DisposeInner(this);

        static async ValueTask DisposeInner(Lease lease)
        {
            await lease._timer.DisposeAsync();
            await lease._cts.CancelAsync();
            lease._cts.Dispose();

            var ticket = Volatile.Read(ref lease._ticket);
            await lease._leaseProvider.ReleaseLease(ticket, cancellationToken: CancellationToken.None);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Warning, "Lease '{LeaseId}' was not disposed. Created at: {Source}")]
        public static partial void LeaseNotDisposed(ILogger logger, string leaseId, StackTrace? source);
    }
}
