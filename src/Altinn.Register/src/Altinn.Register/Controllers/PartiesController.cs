#nullable enable

using System.Diagnostics;
using System.Security.Claims;

using Altinn.Platform.Register.Models;
using Altinn.Register.Core.Parties;
using Altinn.Register.Extensions;
using Altinn.Register.Models;
using Altinn.Register.Services.Interfaces;

using AltinnCore.Authentication.Constants;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using V1Models = Altinn.Platform.Register.Models;

namespace Altinn.Register.Controllers
{
    /// <summary>
    /// The parties controller provides access to party information in the SBL Register component.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "InternalOrPlatformAccess")]
    [Route("register/api/v1/parties")]
    public class PartiesController : ControllerBase
    {
        private readonly IV1PartyService _partyClient;
        private readonly IAuthorizationClient _authorization;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartiesController"/> class.
        /// </summary>
        /// <param name="partyClient">The parties service used as a client when calling SBL Bridge.</param>
        /// <param name="authorizationClient">The authorization client</param>
        public PartiesController(IV1PartyService partyClient, IAuthorizationClient authorizationClient)
        {
            _partyClient = partyClient;
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
        public async Task<ActionResult<V1Models.Party>> Get(int partyId, CancellationToken cancellationToken = default)
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

            V1Models.Party? result = await _partyClient.GetPartyById(partyId, cancellationToken);
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
        public async Task<ActionResult<V1Models.Party>> GetByUuid([FromRoute] Guid partyUuid, CancellationToken cancellationToken = default)
        {
            V1Models.Party? party = await _partyClient.GetPartyById(partyUuid, cancellationToken);

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
        [HttpPost("lookup")]
        [Consumes("application/json")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        public async Task<ActionResult<V1Models.Party>> PostPartyLookup([FromBody] PartyLookup partyLookup, CancellationToken cancellationToken = default)
        {
            Debug.Assert(!string.IsNullOrEmpty(partyLookup.Ssn) || !string.IsNullOrEmpty(partyLookup.OrgNo));

            string lookupValue = partyLookup.OrgNo ?? partyLookup.Ssn!;

            V1Models.Party? party = await _partyClient.LookupPartyBySSNOrOrgNo(lookupValue, cancellationToken);

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
        /// <param name="partyComponentOption">Specifies the components that should be included when retrieving party's information.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The identified party names for the corresponding identifiers.</returns>
        [HttpPost("nameslookup")]
        [Consumes("application/json")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        [Produces("application/json")]
        public async Task<ActionResult<PartyNamesLookupResult>> PostPartyNamesLookup(
            [FromBody] PartyNamesLookup partyNamesLookup,
            [FromQuery] PartyComponentOptions partyComponentOption = PartyComponentOptions.None,
            CancellationToken cancellationToken = default)
        {
            if (partyNamesLookup.Parties is null or { Count: 0 })
            {
                return Ok(new PartyNamesLookupResult
                {
                    PartyNames = [],
                });
            }

            List<PartyName> items = await _partyClient.LookupPartyNames(partyNamesLookup.Parties, partyComponentOption, cancellationToken).ToListAsync(cancellationToken);
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
        public async Task<ActionResult<List<V1Models.Party>>> GetPartyListForPartyIds([FromBody] List<int> partyIds, [FromQuery] bool fetchSubUnits = false, CancellationToken cancellationToken = default)
        {
            List<V1Models.Party> parties = await _partyClient.GetPartiesById(partyIds, fetchSubUnits, cancellationToken).ToListAsync(cancellationToken);

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
        public async Task<ActionResult<List<V1Models.Party>>> GetPartyListForPartyUuids([FromBody] List<Guid> partyUuids, [FromQuery] bool fetchSubUnits = false, CancellationToken cancellationToken = default)
        {
            List<V1Models.Party> parties = await _partyClient.GetPartiesById(partyUuids, fetchSubUnits, cancellationToken).ToListAsync(cancellationToken);
            return Ok(parties);
        }

        /// <summary>
        /// Gets a set of party identifiers given a list of party uuids or org.nos.
        /// </summary>
        /// <param name="idsQuery">The party ids.</param>
        /// <param name="uuidsQuery">The party uuids.</param>
        /// <param name="orgNosQuery">The org.nos.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A set of <see cref="PartyIdentifiers"/> for each of the requested parties.</returns>
        [HttpGet("identifiers")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<IAsyncEnumerable<PartyIdentifiers>> GetPartyIdentifiers(
            [FromQuery(Name = "ids")] List<string?>? idsQuery = null,
            [FromQuery(Name = "uuids")] List<string?>? uuidsQuery = null,
            [FromQuery(Name = "orgs")] List<string?>? orgNosQuery = null,
            CancellationToken cancellationToken = default)
        {
            int count = 0;
            List<int>? ids = null;
            List<Guid>? uuids = null;
            List<string>? orgNos = null;

            if (idsQuery is { Count: > 0 })
            {
                ids = new List<int>();
                foreach (var idString in idsQuery.Where(x => x is not null).SelectMany(idsQuery => idsQuery!.Split(',')))
                {
                    if (!int.TryParse(idString, out int id))
                    {
                        ModelState.AddModelError("ids", $"Invalid id: {idString}");
                    }

                    ids.Add(id);
                }

                count += ids.Count;
            }

            if (uuidsQuery is { Count: > 0 })
            {
                uuids = new List<Guid>();
                foreach (var uuidString in uuidsQuery.Where(x => x is not null).SelectMany(uuidsQuery => uuidsQuery!.Split(',')))
                {
                    if (!Guid.TryParse(uuidString, out Guid uuid))
                    {
                        ModelState.AddModelError("uuids", $"Invalid uuid: {uuidString}");
                    }

                    uuids.Add(uuid);
                }

                count += uuids.Count;
            }

            if (orgNosQuery is { Count: > 0 })
            {
                orgNos = new List<string>();
                foreach (var orgNo in orgNosQuery.Where(x => x is not null).SelectMany(orgNosQuery => orgNosQuery!.Split(',')))
                {
                    // TODO: Validate orgNo
                    orgNos.Add(orgNo);
                }

                count += orgNos.Count;
            }

            if (count > 100)
            {
                ModelState.AddModelError(string.Empty, "Maximum number of identifiers is 100");
            }

            if (count == 0)
            {
                ModelState.AddModelError(string.Empty, "At least one of the query parameters 'ids', 'uuids' or 'orgs' must be provided");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var parties = AsyncEnumerable.Empty<V1Models.Party>();

            if (ids is { Count: > 0 })
            {
                parties = parties.Merge(_partyClient.GetPartiesById(ids, cancellationToken));
            }

            if (uuids is { Count: > 0 })
            {
                parties = parties.Merge(_partyClient.GetPartiesById(uuids, cancellationToken));
            }

            if (orgNos is { Count: > 0 })
            {
                parties = parties.Merge(_partyClient.LookupPartiesBySSNOrOrgNos(orgNos, cancellationToken));
            }

            var all = parties
                .DistinctBy(static p => p.PartyId)
                .Select(static p => PartyIdentifiers.Create(p));

            return Ok(all);
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
