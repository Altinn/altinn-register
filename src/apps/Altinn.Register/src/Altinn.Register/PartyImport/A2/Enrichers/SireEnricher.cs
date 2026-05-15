using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Sire;

namespace Altinn.Register.PartyImport.A2.Enrichers;

/// <summary>
/// An enricher that enriches organization parties with information from SIRE.
/// </summary>
internal sealed partial class SireEnricher
    : IA2PartyImportSagaEnrichmentStep
{
    /// <inheritdoc/>
    public static string StepName => "sire";

    /// <inheritdoc/>
    public static bool CanEnrich(A2PartyImportSagaEnrichmentCheckContext context)
        => context.Party is OrganizationRecord { Source.Value: OrganizationSource.RegisteredWithSkatteetaten };

    private readonly ISireClient _sireClient;
    private readonly IPartyPersistence _parties;
    private readonly IExternalRoleDefinitionPersistence _roleDefinitions;
    private readonly ILogger<SireEnricher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SireEnricher"/> class.
    /// </summary>
    public SireEnricher(ISireClient sireClient, IPartyPersistence parties, IExternalRoleDefinitionPersistence roleDefinitions, ILogger<SireEnricher> logger)
    {
        _sireClient = sireClient;
        _parties = parties;
        _roleDefinitions = roleDefinitions;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Run(A2PartyImportSagaEnrichmentRunContext context, CancellationToken cancellationToken)
    {
        Debug.Assert(context.Party is OrganizationRecord);
        Debug.Assert(context.Party is { OrganizationIdentifier.HasValue: true });

        var result = await _sireClient.GetOrganization(context.Party.OrganizationIdentifier.Value, cancellationToken);

        if (result.IsProblem && result.Problem.ErrorCode == Problems.OrganizationNotFound.ErrorCode)
        {
            // Organization not found in SIRE — mark as deleted
            context.Party = ((OrganizationRecord)context.Party) with
            {
                UnitStatus = "slettet",
                IsDeleted = true,
            };
            return;
        }

        result.EnsureSuccess();
        var organization = result.Value;

        // Filter personally taxable entities - but i dont see this information on the SIRE organization model, so maybe this is not needed?
        if (string.Equals(organization.TaxLiabilityType, "personligSkattepliktig", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Party = ((OrganizationRecord)context.Party) with
        {
            DisplayName = organization.Name,
            UnitType = organization.UnitType,
            UnitStatus = organization.UnitStatus,
            IsDeleted = organization.IsDeleted,
            Source = OrganizationSource.RegisteredWithSkatteetaten,
            ModifiedAt = FieldValue.From(organization.LastUpdated),
            MailingAddress = organization.MailingAddress,
        };

        await MapRoleAssignments(context, organization, cancellationToken);
    }

    private async Task MapRoleAssignments(
        A2PartyImportSagaEnrichmentRunContext context,
        SireOrganization organization,
        CancellationToken cancellationToken)
    {
        var relationships = organization.BusinessRelationships;

        if (relationships is not { Count: > 0 })
        {
            context.RoleAssignments[ExternalRoleSource.RegisteredWithSkatteetaten] = [];
            return;
        }

        // Collect identifiers to resolve → ToPartyUuid
        var personIdentifiers = relationships
            .Where(r => r.RelatedPersonIdentifier is not null)
            .Select(r => r.RelatedPersonIdentifier!)
            .Distinct()
            .ToList();

        var orgIdentifiers = relationships
            .Where(r => r.RelatedOrganizationIdentifier is not null)
            .Select(r => r.RelatedOrganizationIdentifier!)
            .Distinct()
            .ToList();

        var relatedParties = await _parties.LookupParties(
            personIdentifiers: personIdentifiers.Count > 0 ? personIdentifiers : null,
            organizationIdentifiers: orgIdentifiers.Count > 0 ? orgIdentifiers : null,
            include: PartyFieldIncludes.PartyUuid | PartyFieldIncludes.PartyPersonIdentifier | PartyFieldIncludes.PartyOrganizationIdentifier,
            cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var personPartyLookup = relatedParties
            .Where(p => p.PersonIdentifier.HasValue)
            .ToDictionary(p => p.PersonIdentifier.Value!, p => p.PartyUuid.Value);

        var orgPartyLookup = relatedParties
            .Where(p => p.OrganizationIdentifier.HasValue)
            .ToDictionary(p => p.OrganizationIdentifier.Value, p => p.PartyUuid.Value);

        var mapped = new List<UpsertExternalRoleAssignmentsCommand.Assignment>(relationships.Count);
        foreach (var rel in relationships)
        {
            var roleDef = await _roleDefinitions.TryGetRoleDefinition(
                ExternalRoleSource.RegisteredWithSkatteetaten,
                rel.RelationshipType,
                cancellationToken);

            if (roleDef is null)
            {
                Log.RoleDefinitionNotFound(_logger, rel.RelationshipType, context.PartyUuid);
                continue;
            }

            Guid toPartyUuid;
            if (rel.RelatedPersonIdentifier is { } personId)
            {
                if (!personPartyLookup.TryGetValue(personId, out toPartyUuid))
                {
                    Log.RelatedPartyNotFound(_logger, rel.RelationshipType, context.PartyUuid, personId.ToString());
                    continue;
                }
            }
            else if (rel.RelatedOrganizationIdentifier is { } orgId)
            {
                if (!orgPartyLookup.TryGetValue(orgId, out toPartyUuid))
                {
                    Log.RelatedPartyNotFound(_logger, rel.RelationshipType, context.PartyUuid, orgId.ToString());
                    continue;
                }
            }
            else
            {
                continue;
            }

            mapped.Add(new()
            {
                Identifier = roleDef.Identifier,
                ToPartyUuid = toPartyUuid,    // ← the related party
                ////FromPartyUuid is context.PartyUuid, set by the saga when publishing UpsertExternalRoleAssignmentsCommand
            });
        }

        context.RoleAssignments[ExternalRoleSource.RegisteredWithSkatteetaten] = mapped;
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Warning, "SIRE role definition '{RelationshipType}' not found for party '{FromPartyUuid}'. Skipping assignment.")]
        public static partial void RoleDefinitionNotFound(ILogger logger, string relationshipType, Guid fromPartyUuid);

        [LoggerMessage(1, LogLevel.Warning, "Related party '{Identifier}' for role '{RelationshipType}' from party '{FromPartyUuid}' not found in register. Skipping.")]
        public static partial void RelatedPartyNotFound(ILogger logger, string relationshipType, Guid fromPartyUuid, string identifier);
    }
}
