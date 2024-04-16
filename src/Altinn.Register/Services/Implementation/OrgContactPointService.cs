using System.Threading.Tasks;

using Altinn.Register.Models;
using Altinn.Register.Services.Interfaces;

namespace Altinn.Register.Services.Implementation;

/// <summary>
/// An implementation of <see cref="IOrgContactPoints"/>
/// </summary>
public class OrgContactPointService : IOrgContactPoints
{
    private readonly IOrganizations _organizationClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgContactPointService"/> class.
    /// </summary>
    public OrgContactPointService(IOrganizations organizationClient)
    {
        _organizationClient = organizationClient;
    }

    /// <inheritdoc />
    public async Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup organisationNumbers)
    {
        return await _organizationClient.GetContactPoints(organisationNumbers);
    }
}
