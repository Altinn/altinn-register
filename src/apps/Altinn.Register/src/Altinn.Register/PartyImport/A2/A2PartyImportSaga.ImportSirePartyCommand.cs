using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga handler for SIRE-driven party imports. 
/// </summary>
public partial class A2PartyImportSaga
{
    /// <inheritdoc/>
    public static ValueTask<A2PartyImportSagaData> CreateInitialState(IServiceProvider services, ImportSirePartyCommand command)
        => ValueTask.FromResult(new A2PartyImportSagaData
        {
            PartyIdentifier = command.OrganizationIdentifier,
            Tracking = command.Tracking,
        });

    /// <inheritdoc/>
    public async Task Handle(ImportSirePartyCommand message, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        State.PartyIdentifier.TryGetValue(out OrganizationIdentifier? organizationIdentifier);

        Debug.Assert(organizationIdentifier is not null && organizationIdentifier == message.OrganizationIdentifier);
        State.Party = new OrganizationRecord
        {
            // party fields — placeholders, the enrichment chain fills these in
            PartyUuid = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            PartyId = FieldValue.Unset,
            ExternalUrn = PartyExternalRefUrn.OrganizationId.Create(organizationIdentifier),
            DisplayName = FieldValue.Unset,
            PersonIdentifier = FieldValue.Null,
            OrganizationIdentifier = organizationIdentifier,
            CreatedAt = now,
            ModifiedAt = now,
            User = FieldValue.Unset,
            IsDeleted = FieldValue.Unset,
            DeletedAt = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            // organization fields — SireEnricher will populate from SireClient.GetOrganization
            Source = OrganizationSource.RegisteredWithSkatteetaten,
            UnitStatus = FieldValue.Unset,
            UnitType = FieldValue.Unset,
            TelephoneNumber = FieldValue.Unset,
            MobileNumber = FieldValue.Unset,
            FaxNumber = FieldValue.Unset,
            EmailAddress = FieldValue.Unset,
            InternetAddress = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            BusinessAddress = FieldValue.Unset,
        };

        await Enrich(cancellationToken);
    }
}
