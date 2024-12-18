#nullable enable

using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
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
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportConsumer"/> class.
    /// </summary>
    public A2PartyImportConsumer(
        IA2PartyImportService importService,
        ICommandSender commandSender,
        RegisterTelemetry telemetry)
    {
        _importService = importService;
        _sender = commandSender;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2PartyCommand> context)
    {
        var party = await _importService.GetParty(context.Message.PartyUuid, context.CancellationToken);
        await _sender.Send(new UpsertPartyCommand { Party = party }, context.CancellationToken);
        _meters.PartiesFetched.Add(1);
    }

    /// <summary>
    /// Meters for <see cref="A2PartyImportConsumer"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties imported from A2.
        /// </summary>
        public Counter<int> PartiesFetched { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.parties.fetched", "The number of parties fetched from A2.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }
}
