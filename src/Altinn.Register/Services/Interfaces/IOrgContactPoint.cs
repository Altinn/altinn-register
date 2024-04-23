using System.Threading.Tasks;

using Altinn.Register.Models;

namespace Altinn.Register.Services.Interfaces;

/// <summary>
/// Class describing the methods required for organization number contact point service
/// </summary>
public interface IOrgContactPoint
{
    /// <summary>
    /// Method for retriveing contact points for an org 
    /// </summary>
    /// <param name="organizationNumbers">A list of organization numbers to lookup contact points for</param>
    /// <returns>The orgs contact points</returns>
    Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup organizationNumbers);
}
