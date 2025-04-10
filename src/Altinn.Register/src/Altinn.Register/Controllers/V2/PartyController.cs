#nullable enable

using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Core.Utils;
using Altinn.Register.ModelBinding;
using Altinn.Register.Models;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Register.Controllers.V2;

/// <summary>
/// Provides access to parties in the register.
/// </summary>
[ApiController]
[ApiVersion(2.0)]
[Authorize(Policy = "InternalOrPlatformAccess")]
[Route("register/api/v{version:apiVersion}/internal/parties")]
public class PartyController
    : ControllerBase
{
    /// <summary>
    /// The page-size for party streams.
    /// </summary>
    /// <remarks>
    /// Changing this number is *not* a breaking change.
    /// </remarks>
    internal const int PARTY_STREAM_PAGE_SIZE = 100;

    /// <summary>
    /// The page-size for role-assignments streams.
    /// </summary>
    /// <remarks>
    /// Changing this number is *not* a breaking change.
    /// </remarks>
    internal const int ROLEASSIGNMENTS_STREAM_PAGE_SIZE = 1_000;

    /// <summary>
    /// The maximum number of items that can be queried at once.
    /// </summary>
    /// <remarks>
    /// Increasing this number is *not* a breaking change.
    /// Decreasing this number *is* a breaking change.
    /// </remarks>
    internal const int PARTY_QUERY_MAX_ITEMS = 100;

    /// <summary>
    /// Route name for <see cref="GetStream(PartyFieldIncludes, Opaque{ulong}?, CancellationToken)"/>.
    /// </summary>
    public const string ROUTE_GET_STREAM = "parties/stream";

    /// <summary>
    /// Route name for <see cref="GetExternalRoleAssignmentsStream(Opaque{ulong}?, CancellationToken)"/>.
    /// </summary>
    public const string ROUTE_GET_EXTERNALROLE_ASSIGNMENTS_STREAM = "external-roles/assignments/stream";

    private readonly IUnitOfWorkManager _uowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyController"/> class.
    /// </summary>
    public PartyController(IUnitOfWorkManager uowManager)
    {
        _uowManager = uowManager;
    }

    /// <summary>
    /// Gets a stream of parties.
    /// </summary>
    /// <param name="fields">What fields to include.</param>
    /// <param name="token">An optional continuation token.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A stream of all party records.</returns>
    /// <remarks>
    /// The page-size for the stream is not constant, and can change over time. It should not be relied upon.
    /// </remarks>
    [HttpGet("stream", Name = ROUTE_GET_STREAM)]
    public async Task<ActionResult<ItemStream<PartyRecord>>> GetStream(
        [FromQuery(Name = "fields")] PartyFieldIncludes fields = PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName,
        [FromQuery(Name = "token")] Opaque<ulong>? token = null,
        CancellationToken cancellationToken = default)
    {
        const int PAGE_SIZE = PARTY_STREAM_PAGE_SIZE;

        ValidationErrorBuilder errors = default;
        if (fields.HasFlag(PartyFieldIncludes.SubUnits))
        {
            errors.Add(ValidationErrors.PartyFields_SubUnits_Forbidden, "/$QUERY/fields");
        }

        if (errors.TryToActionResult(out var actionResult))
        {
            return actionResult;
        }

        await using var uow = await _uowManager.CreateAsync(cancellationToken);
        var persistence = uow.GetPartyPersistence();

        var maxVersionId = await persistence.GetMaxPartyVersionId(cancellationToken);
        var parties = await persistence.GetPartyStream(
            fromExclusive: token?.Value ?? 0,
            limit: PAGE_SIZE,
            fields | PartyFieldIncludes.PartyVersionId,
            cancellationToken)
            .ToListAsync(cancellationToken);

        string? nextLink = null;
        if (parties.Count > 0)
        {
            nextLink = Url.Link(ROUTE_GET_STREAM, new
            {
                token = Opaque.Create(parties[^1].VersionId.Value),
                fields = PartyFieldIncludesModelBinder.Format(fields),
            });
        }

        return ItemStream.Create(
            parties,
            next: nextLink,
            sequenceMax: maxVersionId,
            sequenceNumberFactory: static p => p.VersionId.Value);
    }

    /// <summary>
    /// Gets a stream of external-role assignments.
    /// </summary>
    /// <param name="token">An optional continuation token.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A stream of all external-role assignment events.</returns>
    [HttpGet("external-roles/assignments/events/stream", Name = ROUTE_GET_EXTERNALROLE_ASSIGNMENTS_STREAM)]
    public async Task<ActionResult<ItemStream<ExternalRoleAssignmentEvent>>> GetExternalRoleAssignmentsStream(
        [FromQuery(Name = "token")] Opaque<ulong>? token = null,
        CancellationToken cancellationToken = default)
    {
        const int PAGE_SIZE = ROLEASSIGNMENTS_STREAM_PAGE_SIZE;

        await using var uow = await _uowManager.CreateAsync(cancellationToken);
        var persistence = uow.GetPartyPersistence();

        var maxVersionId = await persistence.GetMaxExternalRoleAssignmentVersionId(cancellationToken);
        var events = await persistence.GetExternalRoleAssignmentStream(
            fromExclusive: token?.Value ?? 0,
            limit: PAGE_SIZE,
            cancellationToken)
            .ToListAsync(cancellationToken);

        string? nextLink = null;
        if (events.Count > 0)
        {
            nextLink = Url.Link(ROUTE_GET_EXTERNALROLE_ASSIGNMENTS_STREAM, new
            {
                token = Opaque.Create(events[^1].VersionId),
            });
        }

        return ItemStream.Create(
            events,
            next: nextLink,
            sequenceMax: maxVersionId,
            sequenceNumberFactory: static e => e.VersionId);
    }

    private static readonly ExternalRoleReference _revisorRole = new(ExternalRoleSource.CentralCoordinatingRegister, "revisor");

    /// <summary>
    /// Gets all parties that have assigned the "revisor" ccr role to <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid">The revisor party uuid.</param>
    /// <param name="fields">The party fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A set of parties that have assigned the "revisor" ccr role to <paramref name="partyUuid"/>.</returns>
    [ApiVersion(1.0)]
    [ApiVersion(2.0)]
    [HttpGet("{partyUuid:guid}/customers/ccr/revisor")]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status204NoContent)]
    public Task<ActionResult<ListObject<PartyRecord>>> GetRevisorCustomers(
        [FromRoute] Guid partyUuid,
        [FromQuery(Name = "fields")] PartyFieldIncludes fields = PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName,
        CancellationToken cancellationToken = default)
        => GetCustomers(partyUuid, _revisorRole, fields, cancellationToken);

    private static readonly ExternalRoleReference _regnskapsforerRole = new(ExternalRoleSource.CentralCoordinatingRegister, "regnskapsforer");

    /// <summary>
    /// Gets all parties that have assigned the "regnskapsforer" ccr role to <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid">The revisor party uuid.</param>
    /// <param name="fields">The party fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A set of parties that have assigned the "regnskapsforer" ccr role to <paramref name="partyUuid"/>.</returns>
    [ApiVersion(1.0)]
    [ApiVersion(2.0)]
    [HttpGet("{partyUuid:guid}/customers/ccr/regnskapsforer")]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status204NoContent)]
    public Task<ActionResult<ListObject<PartyRecord>>> GetRegnskapsforerCustomers(
        [FromRoute] Guid partyUuid, 
        [FromQuery(Name = "fields")] PartyFieldIncludes fields = PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName,
        CancellationToken cancellationToken = default)
        => GetCustomers(partyUuid, _regnskapsforerRole, fields, cancellationToken);

    private static readonly ExternalRoleReference _forretningsforerRole = new(ExternalRoleSource.CentralCoordinatingRegister, "forretningsforer");

    /// <summary>
    /// Gets all parties that have assigned the "forretningsforer" ccr role to <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid">The revisor party uuid.</param>
    /// <param name="fields">The party fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A set of parties that have assigned the "forretningsforer" ccr role to <paramref name="partyUuid"/>.</returns>
    [ApiVersion(1.0)]
    [ApiVersion(2.0)]
    [HttpGet("{partyUuid:guid}/customers/ccr/forretningsforer")]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status204NoContent)]
    public Task<ActionResult<ListObject<PartyRecord>>> GetForretningsforerCustomers(
        [FromRoute] Guid partyUuid,
        [FromQuery(Name = "fields")] PartyFieldIncludes fields = PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName,
        CancellationToken cancellationToken = default)
        => GetCustomers(partyUuid, _forretningsforerRole, fields, cancellationToken);

    [NonAction]
    private async Task<ActionResult<ListObject<PartyRecord>>> GetCustomers(
        Guid partyUuid,
        ExternalRoleReference role,
        PartyFieldIncludes fields,
        CancellationToken cancellationToken)
    {
        await using var uow = await _uowManager.CreateAsync(cancellationToken);
        var partyPersistence = uow.GetPartyPersistence();
        var rolePersistence = uow.GetPartyExternalRolePersistence();

        List<Guid> customerPartyUuids;

        {
            using var activity = RegisterTelemetry.StartActivity("GetExternalRoleAssignmentsToParty");

            customerPartyUuids = await rolePersistence.GetExternalRoleAssignmentsToParty(
                partyUuid,
                role,
                PartyExternalRoleAssignmentFieldIncludes.RoleFromParty,
                cancellationToken)
                .Select(static r => r.FromParty.Value)
                .ToListAsync(cancellationToken);
        }

        if (customerPartyUuids.Count == 0)
        {
            return StatusCode(StatusCodes.Status204NoContent, ListObject.Create<PartyRecord>([]));
        }

        List<PartyRecord> customers;

        {
            using var activity = RegisterTelemetry.StartActivity("LookupParties");

            customers = await partyPersistence.LookupParties(
                partyUuids: customerPartyUuids,
                include: fields,
                cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);
        }

        if (customers.Count == 0)
        {
            return StatusCode(StatusCodes.Status204NoContent, ListObject.Create<PartyRecord>([]));
        }

        return ListObject.Create(customers);
    }

    private static readonly ExternalRoleReference _dagligLederRole = new(ExternalRoleSource.CentralCoordinatingRegister, "daglig-leder");

    /// <summary>
    /// Gets all parties assigned the "daglig-leder" ccr role by <paramref name="partyUuid"/>.
    /// </summary>
    /// <param name="partyUuid">The party uuid to find "daglig-leder"s for.</param>
    /// <param name="fields">The party fields to include.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A set of parties that have been assigned the "daglig-leder" ccr role from <paramref name="partyUuid"/>.</returns>
    [ApiVersion(1.0)]
    [ApiVersion(2.0)]
    [HttpGet("{partyUuid:guid}/holders/ccr/daglig-leder")]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status204NoContent)]
    public Task<ActionResult<ListObject<PartyRecord>>> GetDagligLedere(
        [FromRoute] Guid partyUuid,
        [FromQuery(Name = "fields")] PartyFieldIncludes fields = PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName,
        CancellationToken cancellationToken = default)
        => GetRoleHolders(partyUuid, _dagligLederRole, fields, cancellationToken);

    [NonAction]
    private async Task<ActionResult<ListObject<PartyRecord>>> GetRoleHolders(
        Guid partyUuid,
        ExternalRoleReference role,
        PartyFieldIncludes fields,
        CancellationToken cancellationToken)
    {
        await using var uow = await _uowManager.CreateAsync(cancellationToken);
        var partyPersistence = uow.GetPartyPersistence();
        var rolePersistence = uow.GetPartyExternalRolePersistence();

        List<Guid> customerPartyUuids;

        {
            using var activity = RegisterTelemetry.StartActivity("GetExternalRoleAssignmentsFromParty");

            customerPartyUuids = await rolePersistence.GetExternalRoleAssignmentsFromParty(
                partyUuid,
                role,
                PartyExternalRoleAssignmentFieldIncludes.RoleToParty,
                cancellationToken)
                .Select(static r => r.ToParty.Value)
                .ToListAsync(cancellationToken);
        }

        List<PartyRecord> holders;

        {
            using var activity = RegisterTelemetry.StartActivity("LookupParties");

            holders = await partyPersistence.LookupParties(
                partyUuids: customerPartyUuids,
                include: fields,
                cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);
        }

        if (holders.Count == 0)
        {
            return StatusCode(StatusCodes.Status204NoContent, ListObject.Create<PartyRecord>([]));
        }

        return ListObject.Create(holders);
    }

    /// <summary>
    /// Looks up parties based on the provided identifiers.
    /// </summary>
    /// <param name="parties">The party identifiers to look up.</param>
    /// <param name="fields">The fields to include in the response.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A set of parties matching the provided identifiers.</returns>
    /// <remarks>
    /// <list type="bullet">
    ///     <item>If a identifier is not found, a successful response (with fewer items) is still returned.</item>
    ///     <item>
    ///     The "type" part of the party UUID URNs are ignored, this means that if a request for <c>altinn:person:uuid:SOME_UUID</c>
    ///     is sent, but <c>SOME_UUID</c> turns out to be a organization UUID, the response will still contain the organization.
    ///     </item>
    /// </list>
    /// </remarks>
    [HttpPost("query")]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ListObject<PartyRecord>>(StatusCodes.Status206PartialContent)]
    public async Task<ActionResult<ListObject<PartyRecord>>> Query(
        [FromBody] ListObject<PartyUrn> parties,
        [FromQuery(Name = "fields")] PartyFieldIncludes fields = PartyFieldIncludes.Identifiers | PartyFieldIncludes.PartyDisplayName,
        CancellationToken cancellationToken = default)
    {
        const int MAX_ITEMS = PARTY_QUERY_MAX_ITEMS;

        ValidationErrorBuilder errors = default;
        if (fields.HasFlag(PartyFieldIncludes.SubUnits))
        {
            errors.Add(ValidationErrors.PartyFields_SubUnits_Forbidden, "/$QUERY/fields");
        }

        List<Guid>? uuids = null;
        List<int>? ids = null;
        List<PersonIdentifier>? personIds = null;
        List<OrganizationIdentifier>? orgIds = null;

        var count = 0;
        foreach (var item in parties.Items)
        {
            switch (item)
            {
                case PartyUrn.PartyId partyId:
                    fields |= PartyFieldIncludes.PartyId;
                    ids ??= new();
                    ids.Add(partyId.Value);
                    break;

                case PartyUrn.PartyUuid partyUuid:
                    fields |= PartyFieldIncludes.PartyUuid;
                    uuids ??= new();
                    uuids.Add(partyUuid.Value);
                    break;

                case PartyUrn.PersonId personId:
                    fields |= PartyFieldIncludes.PartyPersonIdentifier;
                    personIds ??= new();
                    personIds.Add(personId.Value);
                    break;

                case PartyUrn.OrganizationId orgId:
                    fields |= PartyFieldIncludes.PartyOrganizationIdentifier;
                    orgIds ??= new();
                    orgIds.Add(orgId.Value);
                    break;

                default:
                    errors.Add(ValidationErrors.PartyUrn_Invalid, $"/data/{count}");
                    break;
            }

            count++;
        }

        if (count == 0)
        {
            return StatusCode(StatusCodes.Status204NoContent, ListObject.Create<PartyRecord>([]));
        }

        if (count > MAX_ITEMS)
        {
            errors.Add(ValidationErrors.TooManyItems, "/data");
        }

        if (errors.TryToActionResult(out var actionResult))
        {
            return actionResult;
        }

        await using var uow = await _uowManager.CreateAsync(cancellationToken);
        var persistence = uow.GetPartyPersistence();

        var result = await persistence.LookupParties(
            partyUuids: uuids,
            partyIds: ids,
            organizationIdentifiers: orgIds,
            personIdentifiers: personIds,
            fields,
            cancellationToken)
            .ToListAsync(cancellationToken);

        var statusCode = StatusCodes.Status200OK;
        if (result.Count == 0)
        {
            statusCode = StatusCodes.Status204NoContent;
        }
        else
        {
            var anyMissing = ids.OrEmpty().Any(id => !result.Any(p => p.PartyId.Value == id)) 
                || uuids.OrEmpty().Any(uuid => !result.Any(p => p.PartyUuid.Value == uuid)) 
                || orgIds.OrEmpty().Any(orgId => !result.Any(p => p.OrganizationIdentifier.Value == orgId)) 
                || personIds.OrEmpty().Any(personId => !result.Any(p => p.PersonIdentifier.Value == personId));

            if (anyMissing)
            {
                statusCode = StatusCodes.Status206PartialContent;
            }
        }

        return StatusCode(statusCode,  ListObject.Create(result));
    }
}
