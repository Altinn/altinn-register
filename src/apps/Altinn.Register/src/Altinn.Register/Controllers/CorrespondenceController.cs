#nullable enable

using System.Diagnostics;
using System.Runtime.InteropServices;
using Altinn.Register.Contracts;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Models;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Register.Controllers;

/// <summary>
/// Temporary dialogporten API endpoints.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = "InternalOrPlatformAccess")]
[Route("register/api/v{version:apiVersion}/correspondence/parties")]
public class CorrespondenceController(IUnitOfWorkManager uowManager)
    : ControllerBase
{
    /// <summary>
    /// Gets roles used by correspondence from a party.
    /// </summary>
    /// <param name="partyUuid">The party uuid.</param>
    /// <param name="token">Continuation token (used in pagination).</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A page of roles.</returns>
    [HttpGet("{partyUuid:guid}/roles/correspondence-roles")]
    public async Task<ActionResult<Paginated<ExternalRoleAssignment>>> GetCorrespondenceRoles(
        Guid partyUuid,
        [FromQuery(Name = "token")] Opaque<ulong>? token = null,
        CancellationToken cancellationToken = default)
    {
        await using var uow = await uowManager.CreateAsync(cancellationToken);
        var persistence = uow.GetPartyExternalRolePersistence();

        var fromParty = new PartyRef { Uuid = partyUuid };

        var roleRefs = new Dictionary<(ExternalRoleSource Source, string Identifier), ExternalRoleRef>();
        var assignments = new List<ExternalRoleAssignment>();
        await foreach (var assignment in persistence.GetExternalRoleAssignmentsFromParty(partyUuid, cancellationToken: cancellationToken))
        {
            Debug.Assert(assignment.Source.HasValue);
            Debug.Assert(assignment.Identifier.HasValue);
            Debug.Assert(assignment.ToParty.HasValue);
            Debug.Assert(assignment.FromParty.HasValue && assignment.FromParty.Value == partyUuid);

            var isCorrespondenceRole = assignment switch 
            {
                { Source.Value: not ExternalRoleSource.CentralCoordinatingRegister } => false,
                { Identifier.Value: "innehaver" or "komplementar" or "styreleder" or "deltaker-delt-ansvar" or "deltaker-fullt-ansvar" or "bestyrende-reder" or "daglig-leder" or "bostyrer" } => true,
                _ => false,
            };

            if (!isCorrespondenceRole)
            {
                // we only care about correspondence roles
                continue;
            }

            ref var roleDef = ref CollectionsMarshal.GetValueRefOrAddDefault(roleRefs, (assignment.Source.Value, assignment.Identifier.Value), out var exists);
            if (!exists)
            {
                roleDef = new ExternalRoleRef
                {
                    Source = assignment.Source.Value,
                    Identifier = assignment.Identifier.Value
                };
            }

            assignments.Add(new ExternalRoleAssignment
            {
                Role = roleDef!,
                FromParty = fromParty,
                ToParty = new PartyRef { Uuid = assignment.ToParty.Value },
            });
        }

        // No pagination for now
        return Paginated.Create(assignments, next: null);
    }
}
