namespace Altinn.Authorization.ServiceDefaults.Leases;

/// <summary>
/// A provider of leases. Leases are a synchronization primitive that can be used to ensure
/// that only one process is performing a certain task at a time.
/// </summary>
public interface ILeaseProvider 
{
    /// <summary>
    /// Attempts to acquire a lease.
    /// </summary>
    /// <param name="leaseId">The lease id.</param>
    /// <param name="duration">
    /// The duration the lease should be valid for. Prefer shorter durations to avoid
    /// leases being held for a very long time if the process crashes.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <summary>
        /// Attempts to acquire the lease identified by <paramref name="leaseId"/> for the specified <paramref name="duration"/> without applying an unacquired-duration filter.
        /// </summary>
        /// <param name="leaseId">Identifier of the lease to acquire.</param>
        /// <param name="duration">Requested lease duration.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="LeaseAcquireResult"/> indicating the outcome of the acquisition attempt.</returns>
    Task<LeaseAcquireResult> TryAcquireLease(
        string leaseId,
        TimeSpan duration,
        CancellationToken cancellationToken)
        => TryAcquireLease(leaseId, duration, ifUnacquiredFor: null, cancellationToken);

    /// <summary>
    /// Attempts to acquire a lease.
    /// </summary>
    /// <param name="leaseId">The lease id.</param>
    /// <param name="duration">
    /// The duration the lease should be valid for. Prefer shorter durations to avoid
    /// leases being held for a very long time if the process crashes.
    /// </param>
    /// <param name="ifUnacquiredFor">A filter that can be used to reject leases based on whether the lease has been unacquired for a certain amount of time.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <summary>
        /// Attempts to acquire the named lease for the specified duration, optionally requiring the lease to have been unacquired for at least the given interval before acquisition.
        /// </summary>
        /// <param name="leaseId">Identifier of the lease to acquire.</param>
        /// <param name="duration">Requested validity period for the acquired lease.</param>
        /// <param name="ifUnacquiredFor">
        /// If set, the acquisition will only succeed when the lease has been unacquired for at least this duration; when null no unacquired-duration requirement is applied.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="LeaseAcquireResult"/> describing the outcome of the acquisition attempt.</returns>
    Task<LeaseAcquireResult> TryAcquireLease(
        string leaseId,
        TimeSpan duration,
        TimeSpan? ifUnacquiredFor = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to renew a lease.
    /// </summary>
    /// <param name="lease">The <see cref="LeaseTicket"/> of the lease to renew.</param>
    /// <param name="duration">
    /// The duration the lease should be valid for. Prefer shorter durations to avoid
    /// leases being held for a very long time if the process crashes.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="LeaseAcquireResult"/>.</returns>
    Task<LeaseAcquireResult> TryRenewLease(LeaseTicket lease, TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a lease.
    /// </summary>
    /// <param name="lease">The <see cref="LeaseTicket"/> of the lease to release.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns><see langword="true"/> if the lease was successfully released, otherwise <see langword="false"/>.</returns>
    Task<LeaseReleaseResult> ReleaseLease(LeaseTicket lease, CancellationToken cancellationToken = default);
}