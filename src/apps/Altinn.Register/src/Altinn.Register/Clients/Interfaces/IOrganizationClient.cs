using Altinn.Register.Contracts.V1;
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
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        Task<Organization> GetOrganization(string orgNr, CancellationToken cancellationToken = default);

        /// <summary>
        /// Method for retrieving contact points for an org 
        /// </summary>
        /// <param name="lookup">Organization lookup object</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The orgs contact points</returns>
        Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup lookup, CancellationToken cancellationToken = default);
    }
}
