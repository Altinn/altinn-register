using Altinn.Register.Models;
using Altinn.Register.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Register.Controllers;

/// <summary>
/// Controller for organization contact point API endpoints for internal consumption (e.g. Notifications) requiring neither authenticated user token nor access token authorization.
/// </summary>
[ApiController]
[Route("register/api/v1/organizations/contactpoint")]
[ApiExplorerSettings(IgnoreApi = true)]
[Consumes("application/json")]
[Produces("application/json")]
public class OrgContactPointController : ControllerBase
{
    private readonly IOrgContactPoint _orgContactPointService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgContactPointController"/> class.
    /// </summary>
    /// <param name="orgContactPointService">The organization contact point service.</param>
    public OrgContactPointController(IOrgContactPoint orgContactPointService)
    {
        _orgContactPointService = orgContactPointService;
    }

    /// <summary>
    /// Endpoint looking up the contact points for the orgs connected to the provided organization numbers in the request body
    /// </summary>
    /// <returns>Returns an overview of the contact points for all orgs</returns>
    [HttpPost("lookup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrgContactPointsList>> PostLookup(
        [FromBody] OrgContactPointLookup orgContactPointLookup,
        CancellationToken cancellationToken = default)
    {
        return await _orgContactPointService.GetContactPoints(orgContactPointLookup, cancellationToken);
    }
}
