#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.UnitOfWork;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for validating parties from different sources and sending them for importing.
/// </summary>
public sealed class PartyImportValidationConsumer
    : IConsumer<UpsertPartyCommand>
{
    private ICommandSender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyImportValidationConsumer"/> class.
    /// </summary>
    public PartyImportValidationConsumer(ICommandSender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Consumes an <see cref="UpsertPartyCommand"/>. Validates the party and sends it to the batch consumer.
    /// </summary>
    /// <param name="context">The consume context.</param>
    public async Task Consume(ConsumeContext<UpsertPartyCommand> context)
    {
        PartyImportHelper.ValidatePartyForUpset(context.Message.Party);

        await _sender.Send(UpsertValidatedPartyCommand.From(context.Message), context.CancellationToken);
    }

    /// <summary>
    /// Consumer definition for <see cref="PartyImportBatchConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<PartyImportValidationConsumer>
    {
        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<PartyImportValidationConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);

            endpointConfigurator.PrefetchCount = 200;
        }
    }
}
