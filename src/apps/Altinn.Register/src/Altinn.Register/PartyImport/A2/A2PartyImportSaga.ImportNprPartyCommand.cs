using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public partial class A2PartyImportSaga
{
    /// <inheritdoc/>
    public static ValueTask<A2PartyImportSagaData> CreateInitialState(IServiceProvider services, ImportNprPartyCommand command)
        => ValueTask.FromResult(new A2PartyImportSagaData
        {
            PartyIdentifier = command.PersonIdentifier,
            Tracking = command.Tracking,
        });

    /// <inheritdoc/>
    public async Task Handle(ImportNprPartyCommand message, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        State.PartyIdentifier.TryGetValue(out PersonIdentifier? personIdentifier);

        Debug.Assert(personIdentifier is not null && personIdentifier == message.PersonIdentifier);
        State.Party = new PersonRecord
        {
            PartyUuid = FieldValue.Unset,
            OwnerUuid = FieldValue.Null,
            PartyId = FieldValue.Unset,
            ExternalUrn = PartyExternalRefUrn.PersonId.Create(personIdentifier),
            DisplayName = FieldValue.Unset,
            PersonIdentifier = personIdentifier,
            OrganizationIdentifier = FieldValue.Null,
            CreatedAt = now,
            ModifiedAt = now,
            User = FieldValue.Unset,
            IsDeleted = FieldValue.Unset,
            DeletedAt = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            Source = PersonSource.NationalPopulationRegister,
            FirstName = FieldValue.Unset,
            MiddleName = FieldValue.Unset,
            LastName = FieldValue.Unset,
            ShortName = FieldValue.Unset,
            Address = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            DateOfBirth = FieldValue.Unset,
            DateOfDeath = FieldValue.Unset,
        };

        await Enrich(cancellationToken);
    }
}
