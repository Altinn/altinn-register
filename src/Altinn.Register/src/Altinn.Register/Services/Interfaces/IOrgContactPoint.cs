using Altinn.Register.Models;

namespace Altinn.Register.Services.Interfaces;

/// <summary>
/// Class describing the methods required for organization number contact point service
/// </summary>
public interface IOrgContactPoint
{
    /// <summary>
    /// Method for retrieving contact points for an org 
    /// </summary>
    /// <param name="lookup">Organization lookup object</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The orgs contact points</returns>
    Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup lookup, CancellationToken cancellationToken);
}
