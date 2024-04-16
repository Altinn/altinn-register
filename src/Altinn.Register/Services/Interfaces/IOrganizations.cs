using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Models;

namespace Altinn.Register.Services.Interfaces
{
    /// <summary>
    /// Interface handling methods for operations related to organizations
    /// </summary>
    public interface IOrganizations
    {
        /// <summary>
        /// Method that fetches a organization based on a organization number
        /// </summary>
        /// <param name="orgNr">The organization number</param>
        /// <returns></returns>
        Task<Organization> GetOrganization(string orgNr);

        /// <summary>
        /// Method for retriveing contact points for an org 
        /// </summary>
        /// <param name="organisationNumbers">A list of organisation numbers to lookup contact points for</param>
        /// <returns>The orgs contact points</returns>
        Task<OrgContactPointsList> GetContactPoints(List<string> organisationNumbers);
    }
}
