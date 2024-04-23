using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Models;

namespace Altinn.Register.Clients.Interfaces
{
    /// <summary>
    /// Interface handling methods for operations related to organizations
    /// </summary>
    public interface IOrganizationClient
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
        /// <param name="organizationNumbers">A list of organization numbers to lookup contact points for</param>
        /// <returns>The orgs contact points</returns>
        Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup organizationNumbers);
    }
}
