using System.Collections.Immutable;
using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
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
    private readonly ILogger<SireEnricher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SireEnricher"/> class.
    /// </summary>
    public SireEnricher(ISireClient sireClient, ILogger<SireEnricher> logger)
    {
        _sireClient = sireClient;
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
                UnitStatus = "S",
                IsDeleted = true,
            };
            return;
        }

        result.EnsureSuccess();
        var organization = result.Value;

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
            context.RoleAssignments[ExternalRoleSource.RegisteredWithSkatteetaten] = PartyExternalRoleAssignmentsUpdate.Full.Empty
;
            return;
        }

        var mapped = ImmutableArray.CreateBuilder<PartyExternalRoleAssignment>(relationships.Count);
        foreach (var rel in relationships)
        {
            PartyExternalRoleAssignmentPartyRef toParty;
            if (rel.RelatedPersonIdentifier is { } personId)
            {
                toParty = new PartyExternalRoleAssignmentPartyRef.Person
                {
                    PersonIdentifier = personId,
                    Name = null,
                    MailingAddress = null,
                };
            }
            else if (rel.RelatedOrganizationIdentifier is { } orgId)
            {
                toParty = new PartyExternalRoleAssignmentPartyRef.Organization
                {
                    OrganizationIdentifier = orgId,
                };
            }
            else
            {
                ////How should this be handled?
                continue;
            }

            mapped.Add(new()
            {
                ExternalRoleIdentifier = rel.RoleIdentifier,
                ToParty = toParty,
            });
        }

        // Note: For idempotency, this should not use Add, but rather overwrite any existing assignments from the same source
        context.RoleAssignments[ExternalRoleSource.RegisteredWithSkatteetaten] = new PartyExternalRoleAssignmentsUpdate.Full { Assignments = mapped.DrainToImmutableValueArray() };
    }
}
