using System.Threading.Tasks;
using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Models;
using Altinn.Register.Services.Interfaces;

namespace Altinn.Register.Services.Implementation;

/// <summary>
/// An implementation of <see cref="IOrgContactPoint"/>
/// </summary>
public class OrgContactPointService : IOrgContactPoint
{
    private readonly IOrganizationClient _organizationClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgContactPointService"/> class.
    /// </summary>
    public OrgContactPointService(IOrganizationClient organizationClient)
    {
        _organizationClient = organizationClient;
    }

    /// <inheritdoc />
    public async Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup lookup)
    {
        return await _organizationClient.GetContactPoints(lookup);
    }
}
