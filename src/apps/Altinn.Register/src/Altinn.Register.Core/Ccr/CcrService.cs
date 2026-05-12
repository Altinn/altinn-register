using System.Buffers;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.Location;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using CommunityToolkit.Diagnostics;
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
    private readonly IExternalRoleDefinitionPersistence _roleMapper;
    private readonly ILocationLookupProvider _locationLookupProvider;
    private readonly IPartyExternalRolePersistence _rolePersistence;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcrService"/> class.
    /// </summary>
    /// <param name="uowManager">The unit of work manager.</param>
    /// <param name="ccrXmlProcessor">Processes the CCR XML and returns a list of updates</param>
    /// <param name="roleMapper">Maps CCR rolecodes to Altinn Role Codes</param>
    /// <param name="locationLookupProvider">Gets static countrycode lookup</param>
    /// <param name="rolePersistence">Role persistence</param>
    public CcrService(
        IUnitOfWorkManager uowManager,
        ICcrXmlProcessor ccrXmlProcessor,
        IExternalRoleDefinitionPersistence roleMapper,
        ILocationLookupProvider locationLookupProvider,
        IPartyExternalRolePersistence rolePersistence)
    {
        _uowManager = uowManager;
        _ccrXmlProcessor = ccrXmlProcessor;
        _roleMapper = roleMapper;
        _locationLookupProvider = locationLookupProvider;
        _rolePersistence = rolePersistence;
    }

    /// <summary>
    /// Processes a CCR XML batch and upserts all party updates into the database.
    /// </summary>
    /// <param name="commandId">Idempotency disambiguation id for queueing</param>
    /// <param name="input">The raw CCR XML byte sequence.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    public async Task UpdateFromCcr(
        Guid commandId,
        ReadOnlySequence<byte> input,
        CancellationToken cancellationToken)
    {
        ILocationLookup locationLookup = await _locationLookupProvider.GetLocationLookup(cancellationToken);
        IExternalRoleDefinitionLookup roleMap = await _roleMapper.GetRoleDefinitionLookup(cancellationToken);
        var updates = _ccrXmlProcessor.ProcessCcrXml(input, roleMap, locationLookup, cancellationToken);
        var dbUpdates = MapToDbUpdates(updates);

        await using var uow = await _uowManager.CreateAsync(cancellationToken);

        var persistence = uow.GetRequiredService<IPartyPersistence>();

        foreach (CcrDbUpdate dbUpdate in dbUpdates)
        {
            var result = await persistence.UpsertParty(dbUpdate.Org, cancellationToken);
            result.EnsureSuccess();

            if (dbUpdate.RolesUpdate is not null)
            {
                await _rolePersistence.UpsertExternalRolesFromPartyBySource(
                    commandId,
                    partyUuid: result.Value.PartyUuid.Value,
                    ExternalRoleSource.CentralCoordinatingRegister,
                    dbUpdate.RolesUpdate,
                    cancellationToken);
            }
        }

        await uow.CommitAsync(cancellationToken);
    }

    private static IEnumerable<CcrDbUpdate> MapToDbUpdates(IEnumerable<CcrOrganizationUpdate> updates)
    {
        foreach (var update in updates)
        {
            CcrDbUpdate dbUpdate = new(MapOrganization(update), MapRolesUpdate(update));
            yield return dbUpdate;
        }
    }

    private static PartyExternalRoleAssignmentsUpdate? MapRolesUpdate(CcrOrganizationUpdate update)
    {
        if (update.RoleUpdates is null)
        {
            return null;
        }

        List<string> samuBulks = [];
        List<PartyExternalRoleAssignment> absent = [];
        List<PartyExternalRoleAssignment> present = [];

        PartyExternalRoleAssignmentsUpdate? roles;

        if (update.IsFirstRegistration)
        {
            roles = new PartyExternalRoleAssignmentsUpdate.Full
            {
                Assignments = update.RoleUpdates?.RoleAssignments.
                    Select(a => new PartyExternalRoleAssignment
                    {
                        ExternalRoleIdentifier = a.RoleCode,
                        ToParty = MapToPartyRef(a),
                    }).ToImmutableValueArray() ?? []
            };
        }
        else
        {
            roles = new PartyExternalRoleAssignmentsUpdate.Patch
            {
                AbsentByIdentifier = update.RoleUpdates?.BulkRemoveRoleAssignments.
                    Select(b => b.RoleCode).ToImmutableValueArray() ?? [],

                Absent = update.RoleUpdates?.RemoveRoleAssignments.
                    Select(r => new PartyExternalRoleAssignment
                    {
                        ExternalRoleIdentifier = r.RoleCode,
                        ToParty = MapToPartyRef(r),
                    }).ToImmutableValueArray() ?? [],

                Present = update.RoleUpdates?.RoleAssignments.
                    Select(a => new PartyExternalRoleAssignment
                    {
                        ExternalRoleIdentifier = a.RoleCode,
                        ToParty = MapToPartyRef(a),
                    }).ToImmutableValueArray() ?? []
            };
        }

        return roles;
    }

    private static PartyExternalRoleAssignmentPartyRef MapToPartyRef(CcrRoleAssignment assignment)
    {
        if (assignment.IsToOrganization)
        {
            return new PartyExternalRoleAssignmentPartyRef.Organization
            {
                OrganizationIdentifier = assignment.RoleOrganizationNumber,
            };
        }

        if (assignment.IsToPerson)
        {
            return new PartyExternalRoleAssignmentPartyRef.Person
            {
                PersonIdentifier = assignment.RolePersonalIdentifier,
                Name = assignment.PersonName,
                MailingAddress = assignment.Postadresse,
            };
        }

        ThrowHelper.ThrowArgumentException(nameof(assignment), "Role assignment must be to either a person or an organization, but was neither.");
        return null!; // Unreachable, but required for compilation
    }

    private static OrganizationRecord MapOrganization(CcrOrganizationUpdate model)
    {
        TimeOnly midnight = new TimeOnly(0, 0);
        TimeSpan utcOffset = TimeSpan.Zero; // TODO

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
            ModifiedAt = new DateTimeOffset(model.DatoSistEndret, midnight, utcOffset),
            IsDeleted = model.IsDeleted,
            DeletedAt = FieldValue.From(model.DeletedAt).Select(date => new DateTimeOffset(date, midnight, utcOffset)),
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
            BusinessAddress = model.BusinessAddress,
        };
    }
}

/// <summary>
/// Represents an update to an organization's CCR (Central Contractor Registration) information, including organization
/// details and optional external role assignments.
/// Intended to be used as a data transfer object for upserting party information into the database based on CCR updates.
/// </summary>
/// <param name="Org">The organization record containing updated information for the CCR entry.</param>
/// <param name="RolesUpdate">The set of external role assignments to update for the party, or null to leave roles unchanged.</param>
public record struct CcrDbUpdate(OrganizationRecord Org, PartyExternalRoleAssignmentsUpdate? RolesUpdate);
