using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Register.Models;

namespace Altinn.Register.Services.Interfaces;

/// <summary>
/// Class describing the methods required for org contact point service
/// </summary>
public interface IOrgContactPoint
{
    /// <summary>
    /// Method for retriveing contact points for an org 
    /// </summary>
    /// <param name="organisationNumbers">A list of organisation numbers to lookup contact points for</param>
    /// <returns>The orgs contact points</returns>
    Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup organisationNumbers);
}
