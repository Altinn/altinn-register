using System.Diagnostics;
using Altinn.Register.Contracts;
using Altinn.Register.Core;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;

namespace Altinn.Register.PartyImport.A2.Enrichers;

/// <summary>
/// Enriches organizations with roles from CCR.
/// </summary>
internal sealed partial class CcrRoleAssignmentsEnricher
    : IA2PartyImportSagaEnrichmentStep
{
    /// <inheritdoc/>
    public static string StepName
        => "ccr-roles";

    /// <inheritdoc/>
    public static bool CanEnrich(A2PartyImportSagaEnrichmentCheckContext context)
        => context.Party is OrganizationRecord { Source.Value: OrganizationSource.CentralCoordinatingRegister };

    private readonly IA2PartyImportService _importService;
    private readonly IExternalRoleDefinitionPersistence _roleDefinitions;
    private readonly ILogger<CcrRoleAssignmentsEnricher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcrRoleAssignmentsEnricher"/> class.
    /// </summary>
    public CcrRoleAssignmentsEnricher(
        IA2PartyImportService importService,
        IExternalRoleDefinitionPersistence roleDefinitions,
        ILogger<CcrRoleAssignmentsEnricher> logger)
    {
        _importService = importService;
        _roleDefinitions = roleDefinitions;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Run(A2PartyImportSagaEnrichmentRunContext context, CancellationToken cancellationToken)
    {
        Debug.Assert(context.Party is OrganizationRecord { Source.Value: OrganizationSource.CentralCoordinatingRegister });
        Debug.Assert(context.Party.PartyId.HasValue);
        var assignments = await _importService.GetExternalRoleAssignmentsFrom(context.Party.PartyId.Value, context.PartyUuid, cancellationToken)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            context.RoleAssignments.Add(ExternalRoleSource.CentralCoordinatingRegister, []);
            return;
        }

        var mapped = new List<UpsertExternalRoleAssignmentsCommand.Assignment>(assignments.Count);
        foreach (var assignment in assignments)
        {
            var roleDefinition = await _roleDefinitions.TryGetRoleDefinitionByRoleCode(assignment.RoleCode, cancellationToken);
            if (roleDefinition is null)
            {
                Log.RoleWithRoleCodeNotFound(_logger, assignment.RoleCode, context.PartyUuid, assignment.ToPartyUuid);
                continue;
            }

            if (roleDefinition.Source != ExternalRoleSource.CentralCoordinatingRegister)
            {
                Log.RoleWithWrongSource(_logger, assignment.RoleCode, context.PartyUuid, assignment.ToPartyUuid, roleDefinition.Source, roleDefinition.Identifier);
                continue;
            }

            mapped.Add(new()
            {
                Identifier = roleDefinition.Identifier,
                ToPartyUuid = assignment.ToPartyUuid,
            });
        }

        // Note: For idempotency, this should not use Add, but rather overwrite any existing assignments from the same source
        context.RoleAssignments[ExternalRoleSource.CentralCoordinatingRegister] = mapped;
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Warning, "External role-definition with role code '{RoleCode}' not found. Attempted to add from party '{FromPartyUuid}' to party '{ToPartyUuid}'.")]
        public static partial void RoleWithRoleCodeNotFound(ILogger logger, string roleCode, Guid fromPartyUuid, Guid toPartyUuid);

        [LoggerMessage(1, LogLevel.Warning, "External role-definition with role code '{RoleCode}' found, but with the wrong source '{Source}' and identifier '{Identifier}'. Attempted to add from party '{FromPartyUuid}' to party '{ToPartyUuid}'.")]
        public static partial void RoleWithWrongSource(ILogger logger, string roleCode, Guid fromPartyUuid, Guid toPartyUuid, ExternalRoleSource source, string identifier);
    }
}
