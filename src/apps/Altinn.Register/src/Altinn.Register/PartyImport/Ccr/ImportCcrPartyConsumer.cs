using System.Buffers;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Ccr;
using MassTransit;

namespace Altinn.Register.PartyImport.Ccr;

/// <summary>
/// Consumes <see cref="ImportCcrPartyCommand"/> messages produced by the
/// <see cref="CcrImportJob"/> and applies each organization update through the
/// <see cref="CcrService"/>.
/// </summary>
public sealed partial class ImportCcrPartyConsumer
    : IConsumer<ImportCcrPartyCommand>
{
    private readonly CcrService _ccrService;
    private readonly ILogger<ImportCcrPartyConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportCcrPartyConsumer"/> class.
    /// </summary>
    public ImportCcrPartyConsumer(
        CcrService ccrService,
        ILogger<ImportCcrPartyConsumer> logger)
    {
        _ccrService = ccrService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task Consume(ConsumeContext<ImportCcrPartyCommand> context)
    {
        var message = context.Message;

        Log.ConsumingCcrUpdate(_logger, message.OrganizationIdentifier);

        return _ccrService.UpdateFromCcr(
            commandId: message.CommandId,
            input: new ReadOnlySequence<byte>(message.Document),
            cancellationToken: context.CancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Consuming CCR update for organization {OrganizationIdentifier}.")]
        public static partial void ConsumingCcrUpdate(ILogger logger, OrganizationIdentifier organizationIdentifier);
    }
}
