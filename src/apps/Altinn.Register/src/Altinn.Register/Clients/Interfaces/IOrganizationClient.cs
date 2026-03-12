using Altinn.Register.Contracts.V1;

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
    }
}
