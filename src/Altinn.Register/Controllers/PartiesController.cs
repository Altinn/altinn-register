#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Core.Parties;
using Altinn.Register.Filters;
using Altinn.Register.Services.Interfaces;

using AltinnCore.Authentication.Constants;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Register.Controllers
{
    /// <summary>
    /// The parties controller provides access to party information in the SBL Register component.
    /// </summary>
    [Authorize(Policy = "InternalOrPlatformAccess")]
    [Route("register/api/v1/parties")]
    public class PartiesController : Controller
    {
        private readonly IPartyClient _partyService;
        private readonly IAuthorizationClient _authorization;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartiesController"/> class.
        /// </summary>
        /// <param name="partyService">The parties service used as a client when calling SBL Bridge.</param>
        /// <param name="authorizationClient">The authorization client</param>
        public PartiesController(IPartyClient partyService, IAuthorizationClient authorizationClient)
        {
            _partyService = partyService;
            _authorization = authorizationClient;
        }

        /// <summary>
        /// Gets the party for a given party id.
        /// </summary>
        /// <param name="partyId">The party id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The information about a given party.</returns>
        [HttpGet("{partyId:int}")]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        [Authorize]
        public async Task<ActionResult<Party>> Get(int partyId, CancellationToken cancellationToken = default)
        {
            if (!IsOrg(HttpContext))
            {
                int? userId = GetUserId(HttpContext);
                bool? isValid = false;
                if (userId.HasValue)
                {
                    isValid = PartyIsCallingUser(partyId);
                    if ((bool)!isValid)
                    {
                        isValid = await _authorization.ValidateSelectedParty(userId.Value, partyId, cancellationToken);
                    }
                }

                if (!isValid.HasValue || !isValid.Value)
                {
                    return Unauthorized();
                }
            }

            Party? result = await _partyService.GetPartyById(partyId, cancellationToken);
            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets the party for a given party uuid.
        /// </summary>
        /// <param name="partyUuid">The party uuid.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The information about a given party.</returns>
        [HttpGet("byuuid/{partyUuid:Guid}")]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        [Authorize]
        public async Task<ActionResult<Party>> GetByUuid([FromRoute] Guid partyUuid, CancellationToken cancellationToken = default)
        {
            Party? party = await _partyService.GetPartyById(partyUuid, cancellationToken);

            if (!IsOrg(HttpContext))
            {
                int? userId = GetUserId(HttpContext);
                bool? isValid = false;
                
                if (party != null && userId.HasValue)
                {
                    isValid = PartyIsCallingUser(party.PartyId);
                    
                    if ((bool)!isValid)
                    {
                        isValid = await _authorization.ValidateSelectedParty(userId.Value, party.PartyId, cancellationToken);
                    }
                }
                
                if (!isValid.HasValue || !isValid.Value)
                {
                    return Unauthorized();
                }
            }

            if (party == null)
            {
                return NotFound();
            }

            return Ok(party);
        }

        /// <summary>
        /// Perform a lookup/search for a specific party by using one of the provided ids.
        /// </summary>
        /// <param name="partyLookup">The lookup criteria. One and only one of the properties must be a valid value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The identified party.</returns>
        [ValidateModelState]
        [HttpPost("lookup")]
        [Consumes("application/json")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        public async Task<ActionResult<Party>> PostPartyLookup([FromBody] PartyLookup partyLookup, CancellationToken cancellationToken = default)
        {
            string lookupValue = partyLookup.OrgNo ?? partyLookup.Ssn;

            Party? party = await _partyService.LookupPartyBySSNOrOrgNo(lookupValue, cancellationToken);

            if (party == null)
            {
                return NotFound();
            }

            return Ok(party);
        }
        
        /// <summary>
        /// Perform a name lookup for the list of parties for the provided ids.
        /// </summary>
        /// <param name="partyNamesLookup">A list of lookup criteria. For each criteria, one and only one of the properties must be a valid value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The identified party names for the corresponding identifiers.</returns>
        [ValidateModelState]
        [HttpPost("nameslookup")]
        [Consumes("application/json")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        public async Task<ActionResult<PartyNamesLookupResult>> PostPartyNamesLookup([FromBody] PartyNamesLookup partyNamesLookup, CancellationToken cancellationToken = default)
        {
            List<PartyName> items = await _partyService.LookupPartyNames(partyNamesLookup.Parties, cancellationToken).ToListAsync(cancellationToken);
            var partyNamesLookupResult = new PartyNamesLookupResult
            {
                PartyNames = items
            };

            return Ok(partyNamesLookupResult);
        }

        /// <summary>
        /// Gets the party list for the list of party ids.
        /// </summary>
        /// <param name="partyIds">List of partyIds for parties to retrieve.</param>
        /// <param name="fetchSubUnits">flag indicating whether subunits should be included</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>List of parties based on the partyIds.</returns>
        [HttpPost("partylist")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public async Task<ActionResult<List<Party>>> GetPartyListForPartyIds([FromBody] List<int> partyIds, [FromQuery] bool fetchSubUnits = false, CancellationToken cancellationToken = default)
        {
            List<Party> parties = await _partyService.GetPartiesById(partyIds, fetchSubUnits, cancellationToken).ToListAsync(cancellationToken);

            if (parties == null || parties.Count < 1)
            {
                return NotFound();
            }

            return Ok(parties);
        }

        /// <summary>
        /// Gets the party list for the list of party uuids.
        /// </summary>
        /// <param name="partyUuids">List of partyUuids for parties to retrieve.</param>
        /// <param name="fetchSubUnits">flag indicating whether subunits should be included</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>List of parties based on the party uuids.</returns>
        [HttpPost("partylistbyuuid")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public async Task<ActionResult<List<Party>>> GetPartyListForPartyUuids([FromBody] List<Guid> partyUuids, [FromQuery] bool fetchSubUnits = false, CancellationToken cancellationToken = default)
        {
            List<Party> parties = await _partyService.GetPartiesById(partyUuids, fetchSubUnits, cancellationToken).ToListAsync(cancellationToken);
            return Ok(parties);
        }

        /// <summary>
        /// Check whether the party id is the user's party id
        /// </summary>
        private bool PartyIsCallingUser(int partyId)
        {
            Claim? claim = HttpContext.User.Claims.FirstOrDefault(claim => claim.Type.Equals(AltinnCoreClaimTypes.PartyID));
            return claim != null && (int.Parse(claim.Value) == partyId);
        }

        /// <summary>
        /// Validate if the authenticated identity is an org
        /// </summary>
        private static bool IsOrg(HttpContext context)
        {
            return context.User.Claims.Any(claim => claim.Type.Equals(AltinnCoreClaimTypes.Org));
        }

        /// <summary>
        /// Gets userId from httpContext
        /// </summary>
        private static int? GetUserId(HttpContext context)
        {
            Claim? claim = context.User.Claims.FirstOrDefault(claim => claim.Type.Equals(AltinnCoreClaimTypes.UserId));

            return claim != null ? Convert.ToInt32(claim.Value) : null;
        }
    }
}
