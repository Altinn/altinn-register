#nullable enable

using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Core.Leases;

/// <summary>
/// Transient manager for leases.
/// </summary>
public class LeaseManager
{
    private static readonly ObjectFactory<Lease> _factory
        = ActivatorUtilities.CreateFactory<Lease>([typeof(ILeaseProvider), typeof(LeaseTicket), typeof(StackTrace), typeof(CancellationToken)]);

    private readonly ILeaseProvider _provider;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaseManager"/> class.
    /// </summary>
    public LeaseManager(ILeaseProvider provider, IServiceProvider services)
    {
        Guard.IsNotNull(provider);
        Guard.IsNotNull(services);

        _provider = provider;
        _services = services;
    }

    /// <summary>
    /// Gets a auto-renewing lease for the specified lease id.
    /// </summary>
    /// <remarks>
    /// If <paramref name="cancellationToken"/> is cancelled, the lease will be released.
    /// </remarks>
    /// <param name="leaseId">The lease id.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="Lease"/>, if the lease was available, otherwise <see langword="null"/>.</returns>
    public async Task<Lease?> AcquireLease(string leaseId, CancellationToken cancellationToken = default)
    {
        StackTrace? source = null;

        #if DEBUG
        source = new StackTrace();
        #endif

        LeaseTicket? ticket = null;
        
        try
        {
            var result = await _provider.TryAcquireLease(leaseId, Lease.LeaseRenewalInterval, cancellationToken);

            if (!result.IsLeaseAcquired)
            {
                return null;
            }

            ticket = result.Lease;
            var lease = _factory(_services, [_provider, ticket, source, cancellationToken]);
            
            ticket = null;
            return lease;
        }
        finally
        {
            if (ticket is not null)
            {
                await _provider.ReleaseLease(ticket, cancellationToken: CancellationToken.None);
            }
        }
    }
}
