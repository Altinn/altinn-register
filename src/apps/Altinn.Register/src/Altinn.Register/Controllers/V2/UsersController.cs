using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Operations;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Mapping;
using Altinn.Register.Models;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Register.Controllers.V2;

/// <summary>
/// Provisioning endpoints for self-identified users.
/// </summary>
/// <remarks>
/// Permanent register endpoint introduced ahead of the SBL Bridge decommission
/// (deadline 2026-06-19). Iteration 1 proxies to SBL Bridge; iteration 2 will write
/// directly to the register database. See issue #863.
/// </remarks>
[ApiController]
[ApiVersion(2.0)]
[Authorize(Policy = "PlatformAccess")]
[Route("register/api/v{version:apiVersion}/internal/users")]
public sealed class UsersController : ControllerBase
{
    /// <summary>
    /// Gets the existing self-identified user for the supplied identity, or creates one if none exists.
    /// </summary>
    /// <param name="sender">The mediator request sender.</param>
    /// <param name="request">The request body.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The self-identified user.</returns>
    [HttpPost("self-identified")]
    [ProducesResponseType<SelfIdentifiedUser>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SelfIdentifiedUser>> GetOrCreateSelfIdentifiedUser(
        [FromServices] IRequestSender<GetOrCreateSelfIdentifiedUserRequest, SelfIdentifiedUserRecord> sender,
        [FromBody] SelfIdentifiedUserCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest();
        }

        GetOrCreateSelfIdentifiedUserRequest mediatorRequest = new(
            SelfIdentifiedUserType: request.SelfIdentifiedUserType,
            ExternalIdentity: request.ExternalIdentity,
            UserName: request.UserName,
            Email: request.Email);

        Result<SelfIdentifiedUserRecord> result = await sender.Send(mediatorRequest, cancellationToken);
        if (result.IsProblem)
        {
            return result.Problem.ToActionResult();
        }

        return Ok(result.Value.ToPlatformModel());
    }
}
