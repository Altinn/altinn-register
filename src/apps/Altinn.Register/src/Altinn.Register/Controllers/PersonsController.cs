#nullable enable

using System.Security.Claims;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts.V1;
using Altinn.Register.Core.A2;
using Altinn.Register.Models;
using AltinnCore.Authentication.Constants;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Register.Controllers
{
    /// <summary>
    /// The <see cref="PersonsController"/> provides the API endpoints related to persons.
    /// </summary>
    [ApiController]
    [ApiVersion(1.0)]
    [Authorize(Policy = "PlatformAccess")]
    [Authorize(Policy = "AuthorizationLevel2")]
    [Route("register/api/v{version:apiVersion}/persons")]
    public class PersonsController : ControllerBase
    {
        private readonly IPersonLookup _personLookup;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersonsController"/> class.
        /// </summary>
        /// <param name="personLookup">An implementation of the <see cref="IPersonLookup"/> service.</param>
        public PersonsController(IPersonLookup personLookup)
        {
            _personLookup = personLookup;
        }

        /// <summary>
        /// Gets the <see cref="Person"/> with the given national identity number.
        /// </summary>
        /// <remarks>
        /// This endpoint can be used to retrieve the person object for an identified person. The service
        /// will track the number of failed lookup attempts and block further requests if the number of failed
        /// lookups have exceeded a configurable number. The user will be prevented from performing new searches
        /// for a configurable number of seconds.
        /// </remarks>
        /// <returns>The party of the identified person.</returns>
        [HttpGet]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        public async Task<ActionResult<Person>> GetPerson(
            PersonLookupIdentifiers personLookup,
            CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Guid? userId = GetPartyUuid(HttpContext);

            if (userId is null)
            {
                return Forbid();
            }

            var result = await _personLookup.GetPerson(
                personLookup.NationalIdentityNumber,
                personLookup.LastName,
                userId.Value,
                cancellationToken);

            if (result.IsProblem)
            {
                return result.Problem.ToActionResult();
            }

            return result.Value;
        }

        private static Guid? GetPartyUuid(HttpContext context)
        {
            Claim? userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type.Equals(AltinnCoreClaimTypes.PartyUUID));

            return userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out Guid partyUuid)
                ? partyUuid
                : null;
        }
    }
}
