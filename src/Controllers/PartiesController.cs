using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
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
    [Authorize(Policy = "PlatformAccess")]
    [Route("register/api/v1/parties")]
    public class PartiesController : Controller
    {
        private readonly IParties _partiesWrapper;
        private readonly IAuthorization _authorization;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartiesController"/> class.
        /// </summary>
        /// <param name="partiesWrapper">The parties wrapper used as a client when calling SBL Bridge.</param>
        /// <param name="authorizationWrapper">The authorization wrapper</param>
        public PartiesController(IParties partiesWrapper, IAuthorization authorizationWrapper)
        {
            _partiesWrapper = partiesWrapper;
            _authorization = authorizationWrapper;
        }

        /// <summary>
        /// Gets the party for a given party id.
        /// </summary>
        /// <param name="partyId">The party id.</param>
        /// <returns>The information about a given party.</returns>
        [HttpGet("{partyId:int}")]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        [Authorize]
        public async Task<ActionResult<Party>> Get(int partyId)
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
                        isValid = await _authorization.ValidateSelectedParty(userId.Value, partyId);
                    }
                }

                if (!isValid.HasValue || !isValid.Value)
                {
                    return Unauthorized();
                }
            }

            Party result = await _partiesWrapper.GetParty(partyId);
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
        /// <returns>The information about a given party.</returns>
        [HttpGet("byuuid/{partyUuid:Guid}")]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        [Authorize]
        public async Task<ActionResult<Party>> GetByUuid(Guid partyUuid)
        {
            Party party = await _partiesWrapper.GetPartyByUuid(partyUuid);

            if (!IsOrg(HttpContext))
            {
                int? userId = GetUserId(HttpContext);
                bool? isValid = false;
                
                if (party != null && userId.HasValue)
                {
                    isValid = PartyIsCallingUser(party.PartyId);
                    
                    if ((bool)!isValid)
                    {
                        isValid = await _authorization.ValidateSelectedParty(userId.Value, party.PartyId);
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
        /// <returns>The identified party.</returns>
        [ValidateModelState]
        [HttpPost("lookup")]
        [Consumes("application/json")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        public async Task<ActionResult<Party>> PostPartyLookup([FromBody] PartyLookup partyLookup)
        {
            string lookupValue = partyLookup.OrgNo ?? partyLookup.Ssn;

            Party party = await _partiesWrapper.LookupPartyBySSNOrOrgNo(lookupValue);

            if (party == null)
            {
                return NotFound();
            }

            return Ok(party);
        }

        /// <summary>
        /// Gets the party list for the list of party ids.
        /// </summary>
        /// <param name="partyIds">List of partyIds for parties to retrieve.</param>
        /// <param name="fetchSubUnits">flag indicating whether subunits should be included</param>
        /// <returns>List of parties based on the partyIds.</returns>
        [HttpPost("partylist")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public async Task<ActionResult<List<Party>>> GetPartyListForPartyIds([FromBody] List<int> partyIds, [FromQuery] bool fetchSubUnits = false)
        {
            List<Party> parties;

            parties = await _partiesWrapper.GetPartyList(partyIds, fetchSubUnits);

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
        /// <returns>List of parties based on the party uuids.</returns>
        [HttpPost("partylistbyuuid")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public async Task<ActionResult<List<Party>>> GetPartyListForPartyUuids([FromBody] List<Guid> partyUuids, [FromQuery] bool fetchSubUnits = false)
        {
            List<Party> parties = await _partiesWrapper.GetPartyListByUuid(partyUuids, fetchSubUnits);
            return Ok(parties);
        }

        /// <summary>
        /// Check whether the party id is the user's party id
        /// </summary>
        private bool PartyIsCallingUser(int partyId)
        {
            Claim claim = HttpContext.User.Claims.FirstOrDefault(claim => claim.Type.Equals(AltinnCoreClaimTypes.PartyID));
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
            Claim claim = context.User.Claims.FirstOrDefault(claim => claim.Type.Equals(AltinnCoreClaimTypes.UserId));

            return claim != null ? Convert.ToInt32(claim.Value) : null;
        }
    }
}
