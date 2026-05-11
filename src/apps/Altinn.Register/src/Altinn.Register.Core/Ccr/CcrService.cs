using System.Buffers;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Core.Ccr;

/// <summary>
/// Calls the XML processor for CCR (Customer Contact Register) data and transforms the dto's to
/// to models usable for the db layer.
/// </summary>
public class CcrService
{
    private readonly IUnitOfWorkManager _uowManager;
    private readonly ICcrXmlProcessor _ccrXmlProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcrService"/> class.
    /// </summary>
    /// <param name="uowManager">The unit of work manager.</param>
    /// <param name="ccrXmlProcessor">Processes the CCR XML and returns a list of updates</param>
    public CcrService(
        IUnitOfWorkManager uowManager,
        ICcrXmlProcessor ccrXmlProcessor)
    {
        _uowManager = uowManager;
        _ccrXmlProcessor = ccrXmlProcessor;
    }

    /// <summary>
    /// Processes a CCR XML batch and upserts all party updates into the database.
    /// </summary>
    /// <param name="input">The raw CCR XML byte sequence.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    public async Task UpdateFromCcr(ReadOnlySequence<byte> input, CancellationToken cancellationToken)
    {
        var updates = _ccrXmlProcessor.ProcessCcrXml(input, cancellationToken);
        var dbUpdates = MapToDbUpdates(updates);

        await using var uow = await _uowManager.CreateAsync(cancellationToken);

        var persistence = uow.GetRequiredService<IPartyPersistence>();

        foreach (var dbUpdate in dbUpdates)
        {
            var result = await persistence.UpsertParty(dbUpdate.Org, cancellationToken);
            {
                result.EnsureSuccess();
            }
                        
            if (dbUpdate.Roles is not null && dbUpdate.Roles is PartyExternalRoleAssignmentsUpdate.Patch patch)
            {
                foreach (var assignment in patch.AbsentByIdentifier)
                {
                    // TODO samu bulk removal based on external role identifier
                }

                foreach (var assignment in patch.Absent)
                {
                    // TODO removal based on external role identifier and party reference
                }

                foreach (var assignment in patch.Present)
                {
                    // TODO upsert based on external role identifier and party reference
                }
            }            
        }

        await uow.CommitAsync(cancellationToken);
    }

    private static IEnumerable<CcrDbUpdate> MapToDbUpdates(IEnumerable<CcrOrganizationUpdate> updates)
    {
        foreach (var update in updates)
        {
            CcrDbUpdate dbUpdate = MapToDbUpdate(update);
            yield return dbUpdate;
        }
    }

    private static CcrDbUpdate MapToDbUpdate(CcrOrganizationUpdate update)
    {
        List<string> samuBulks = [];
        List<PartyExternalRoleAssignment> absent = [];
        List<PartyExternalRoleAssignment> present = [];

        if (update.RoleUpdates is null)
        {
            samuBulks = update.RoleUpdates?.BulkRemoveRoleAssignments.Select(b => b.RoleCode).ToList() ?? [];

            absent = update.RoleUpdates?.RemoveRoleAssignments.Select(r => new PartyExternalRoleAssignment
            {
                ExternalRoleIdentifier = r.RoleCode,
                ToParty = SetToParty(r.RolePersonalIdentifier, r.RoleOrganizationNumber)
            }).ToList() ?? [];

            present = update.RoleUpdates?.RoleAssignments.Select(a => new PartyExternalRoleAssignment
            {
                ExternalRoleIdentifier = a.RoleCode,
                ToParty = SetToParty(a.RolePersonalIdentifier, a.RoleOrganizationNumber)
            }).ToList() ?? [];
        }

        PartyExternalRoleAssignmentsUpdate.Patch? roles = new()
        {
            AbsentByIdentifier = samuBulks.ToImmutableValueArray(),
            Absent = absent.ToImmutableValueArray(),
            Present = present.ToImmutableValueArray() 
        };

        if (roles.AbsentByIdentifier.IsEmpty && roles.Absent.IsEmpty && roles.Present.IsEmpty)
        {
            roles = null;
        }

        return new CcrDbUpdate(MapOrganization(update), roles);
    }

    private static PartyExternalRoleAssignmentPartyRef SetToParty(string? rolePersonalIdentifier, string? roleOrganizationNumber)
    {
        if (rolePersonalIdentifier is not null)
        {
            bool success = PersonIdentifier.TryParse(rolePersonalIdentifier, provider: null, out var personIdentifier);
            if (!success || personIdentifier is null)
            {
                throw new ArgumentException($"Invalid personal identifier: {rolePersonalIdentifier}");
            }

            return new PartyExternalRoleAssignmentPartyRef.Person
            {
                PersonIdentifier = personIdentifier,
                Name = null,
                MailingAddress = null
            };
        }
        else if (roleOrganizationNumber is not null)
        {
            bool success = OrganizationIdentifier.TryParse(roleOrganizationNumber, provider: null, out var organizationIdentifier);
            if (!success || organizationIdentifier is null)
            {
                throw new ArgumentException($"Invalid organization identifier: {roleOrganizationNumber}");
            }

            return new PartyExternalRoleAssignmentPartyRef.Organization
            {
                OrganizationIdentifier = organizationIdentifier
            };
        }
        else
        {
            throw new ArgumentException("Either rolePersonalIdentifier or roleOrganizationNumber must be provided, and correctly formatted.");
        }
    }

    private static OrganizationRecord MapOrganization(CcrOrganizationUpdate model)
    {
        return new OrganizationRecord
        {
            PartyUuid = FieldValue.Unset,
            OwnerUuid = FieldValue.Unset,
            PartyId = FieldValue.Unset,
            ExternalUrn = FieldValue.Unset,
            User = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            VersionId = FieldValue.Unset,
            ParentOrganizationUuid = FieldValue.Unset,

            Source = OrganizationSource.CentralCoordinatingRegister,
            PersonIdentifier = FieldValue.Null,
            ModifiedAt = model.DatoSistEndret,
            IsDeleted = model.IsDeleted,
            DeletedAt = FieldValue.From(model.DeletedAt),
            OrganizationIdentifier = model.OrganizationIdentifier,
            DisplayName = model.DisplayName,
            UnitType = model.UnitType,
            UnitStatus = model.UnitStatus,
            TelephoneNumber = model.TelephoneNumber,
            MobileNumber = model.MobileNumber,
            FaxNumber = model.FaxNumber,
            EmailAddress = model.EmailAddress,
            InternetAddress = model.InternetAddress,
            MailingAddress = model.MailingAddress,
            BusinessAddress = model.BusinessAddress
        };
    }
}

/// <summary>
/// Represents an update to an organization's CCR (Central Contractor Registration) information, including organization
/// details and optional external role assignments.
/// Intended to be used as a data transfer object for upserting party information into the database based on CCR updates.
/// </summary>
/// <param name="Org">The organization record containing updated information for the CCR entry.</param>
/// <param name="Roles">The set of external role assignments to update for the party, or null to leave roles unchanged.</param>
public record struct CcrDbUpdate(OrganizationRecord Org, PartyExternalRoleAssignmentsUpdate.Patch? Roles);
