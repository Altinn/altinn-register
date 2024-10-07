#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Altinn.Register.Core.Leases;

/// <summary>
/// A result of attempting to acquire a lease.
/// </summary>
public sealed record LeaseAcquireResult
{
    /// <summary>
    /// A <see cref="LeaseTicket"/>, if the lease was acquired.
    /// </summary>
    public LeaseTicket? Lease { get; }

    /// <summary>
    /// When the lease will expire (regardless of whether it was acquired).
    /// </summary>
    public DateTimeOffset Expires { get; }

    /// <summary>
    /// Gets a value indicating whether a lease was acquired.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Lease))]
    public bool IsLeaseAcquired => Lease != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaseAcquireResult"/> class.
    /// </summary>
    private LeaseAcquireResult(LeaseTicket? lease, DateTimeOffset expires)
    {
        Lease = lease;
        Expires = expires;
    }

    /// <summary>
    /// Converts a <see cref="LeaseTicket"/> to a <see cref="LeaseAcquireResult"/>.
    /// </summary>
    /// <param name="ticket">The lease ticket.</param>
    public static implicit operator LeaseAcquireResult(LeaseTicket ticket)
        => new(ticket, ticket.Expires);

    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> to a <see cref="LeaseAcquireResult"/>
    /// representing a lease that was not acquired.
    /// </summary>
    /// <param name="expires">When the lease expires.</param>
    public static implicit operator LeaseAcquireResult(DateTimeOffset expires)
        => new(null, expires);
}
