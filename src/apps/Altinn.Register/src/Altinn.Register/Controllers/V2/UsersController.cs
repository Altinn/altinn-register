using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Operations;
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
    /// <returns>The user's identifiers (UUID, party id, user id, username, canonical URN).</returns>
    [HttpPost("self-identified")]
    [ProducesResponseType<SelfIdentifiedUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SelfIdentifiedUserResponse>> GetOrCreateSelfIdentifiedUser(
        [FromServices] IRequestSender<GetOrCreateSelfIdentifiedUserRequest, SelfIdentifiedUserResult> sender,
        [FromBody] SelfIdentifiedUserCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest();
        }

        GetOrCreateSelfIdentifiedUserRequest mediatorRequest = new(
            SelfIdentifiedUserType: request.SelfIdentifiedUserType,
            Email: request.Email,
            Issuer: request.Issuer,
            ExternalSubject: request.ExternalSubject,
            UserNamePrefix: request.UserNamePrefix);

        Result<SelfIdentifiedUserResult> result = await sender.Send(mediatorRequest, cancellationToken);
        if (result.IsProblem)
        {
            return result.Problem.ToActionResult();
        }

        var value = result.Value;
        return Ok(new SelfIdentifiedUserResponse
        {
            PartyUuid = value.PartyUuid,
            PartyId = value.PartyId,
            UserId = value.UserId,
            UserName = value.UserName,
            SelfIdentifiedUserType = value.SelfIdentifiedUserType,
            ExternalUrn = value.ExternalUrn,
        });
    }
}
