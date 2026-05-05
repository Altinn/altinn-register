using System.Buffers;
using Altinn.Authorization.ModelUtils;
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

        await foreach (var result in persistence.UpsertParties(dbUpdates, cancellationToken))
        {
            result.EnsureSuccess();
        }

        await uow.CommitAsync(cancellationToken);
    }

    private static IEnumerable<PartyRecord> MapToDbUpdates(IEnumerable<CcrOrganizationUpdate> updates)
    {
        foreach (var update in updates)
        {
            yield return MapToDbUpdate(update);
        }
    }

    private static PartyRecord MapToDbUpdate(CcrOrganizationUpdate update)
    {
        return MapOrganization(update);
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
