using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Sire;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Altinn.Register.PartyImport.Sire;

/// <summary>
/// Consumer that processes <see cref="IngestSireEventCommand"/> messages,
/// looks up the organization from SIRE, maps it to an <see cref="OrganizationRecord"/>,
/// and publishes it to the existing party import pipeline.
/// </summary>
public sealed partial class SireIngestConsumer
    : IConsumer<IngestSireEventCommand>
{
    private readonly ISireClient _sireClient;
    private readonly ICommandSender _sender;
    private readonly ILogger<SireIngestConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SireIngestConsumer"/> class.
    /// </summary>
    public SireIngestConsumer(
        ISireClient sireClient,
        ICommandSender sender,
        ILogger<SireIngestConsumer> logger)
    {
        _sireClient = sireClient;
        _sender = sender;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<IngestSireEventCommand> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        // If the event itself signals deletion, skip the lookup
        if (IsDeleteEvent(message.EventType))
        {
            var deleted = MapAsDeleted(message);
            await Publish(deleted, message, ct);
            return;
        }

        // Look up the organization from SIRE
        var result = await _sireClient.GetOrganization(message.Identifier, ct);

        if (result.IsProblem && result.Problem.ErrorCode == Problems.OrganizationNotFound.ErrorCode)
        {
            // 404 - organization no longer exists in the register
            _logger.LogInformation(
                "Organization {Identifier} not found in SIRE (seq {SequenceNumber}), marking as deleted.",
                message.Identifier,
                message.SequenceNumber);

            var deleted = MapAsDeleted(message);
            await Publish(deleted, message, ct);
            return;
        }

        result.EnsureSuccess();
        var organization = result.Value;

        // Filter out personally taxable entities - they belong to the person domain
        if (string.Equals(organization.TaxLiabilityType, "personligSkattepliktig", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Skipping organization {Identifier} (seq {SequenceNumber}) - personligSkattepliktig.",
                message.Identifier,
                message.SequenceNumber);
            return;
        }

        var record = Map(organization);
        await Publish(record, message, ct);
    }

    private Task Publish(OrganizationRecord record, IngestSireEventCommand message, CancellationToken ct)
        => _sender.Send(
            new UpsertValidatedPartyCommand
            {
                Party = record,
                Tracking = new UpsertPartyTracking("sire-ingest", (ulong)message.SequenceNumber)
            },
            ct);

    private static OrganizationRecord Map(SireOrganization organization)
        => new()
        {
            DisplayName = organization.Name,
            ExternalUrn = FieldValue.Unset,
            OwnerUuid = FieldValue.Unset,
            PartyUuid = FieldValue.Unset,
            PartyId = FieldValue.Unset,
            OrganizationIdentifier = organization.OrganizationIdentifier,
            PersonIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = organization.LastUpdated.Value,
            IsDeleted = organization.IsDeleted,
            DeletedAt = FieldValue.Unset,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            Source = OrganizationSource.RegisteredWithSkatteetaten,
            UnitType = organization.UnitType,
            UnitStatus = organization.UnitStatus,
            TelephoneNumber = FieldValue.Unset,
            MobileNumber = FieldValue.Unset,
            FaxNumber = FieldValue.Unset,
            EmailAddress = FieldValue.Unset,
            InternetAddress = FieldValue.Unset,
            MailingAddress = organization.MailingAddress,
            BusinessAddress = FieldValue.Unset,
        };

    private static OrganizationRecord MapAsDeleted(IngestSireEventCommand message)
        => new()
        {
            OwnerUuid = FieldValue.Unset,            
            ExternalUrn = FieldValue.Unset,
            PartyUuid = FieldValue.Unset,
            PartyId = FieldValue.Unset,
            DisplayName = FieldValue.Unset,
            OrganizationIdentifier =message.Identifier,
            PersonIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = FieldValue.Unset,
            IsDeleted = true,
            DeletedAt = FieldValue.Unset,
            User = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            Source = OrganizationSource.RegisteredWithSkatteetaten,
            UnitType = FieldValue.Unset,
            UnitStatus = "slettet",
            TelephoneNumber = FieldValue.Unset,
            MobileNumber = FieldValue.Unset,
            FaxNumber = FieldValue.Unset,
            EmailAddress = FieldValue.Unset,
            InternetAddress = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            BusinessAddress = FieldValue.Unset,
        };

    private static bool IsDeleteEvent(string eventType)
        => string.Equals(eventType, "slettet", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Consumer definition for <see cref="SireIngestConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<SireIngestConsumer>
    {
        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<SireIngestConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);

            //consumerConfigurator.Options<BatchOptions>(o => o
            //    .SetConcurrencyLimit(5));

            //endpointConfigurator.PrefetchCount = 20;
            endpointConfigurator.ConcurrentMessageLimit = 3;
        }
    }
}
