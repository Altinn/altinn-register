#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core.PartyImport.A2;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for importing parties from A2.
/// </summary>
public sealed class A2PartyImportConsumer
    : IConsumer<ImportA2PartyCommand>
{
    private readonly IA2PartyImportService _importService;
    private readonly ICommandSender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportConsumer"/> class.
    /// </summary>
    public A2PartyImportConsumer(IA2PartyImportService importService, ICommandSender commandSender)
    {
        _importService = importService;
        _sender = commandSender;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2PartyCommand> context)
    {
        var party = await _importService.GetParty(context.Message.PartyUuid, context.CancellationToken);
        await _sender.Send(new UpsertPartyCommand { Party = party }, context.CancellationToken);
    }
}
