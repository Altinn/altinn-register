#nullable enable

using System.Diagnostics.Metrics;
using Altinn.Register.Contracts.Events;
using Altinn.Register.Core;
using Altinn.Register.Core.UnitOfWork;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for upserting parties from different sources.
/// </summary>
public sealed class PartyImportConsumer
    : IConsumer<UpsertPartyCommand>
{
    private readonly IUnitOfWorkManager _uow;
    private readonly ImportMeters _meters;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyImportConsumer"/> class.
    /// </summary>
    public PartyImportConsumer(IUnitOfWorkManager uow, RegisterTelemetry telemetry)
    {
        _uow = uow;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<UpsertPartyCommand> context)
    {
        await using var uow = await _uow.CreateAsync(cancellationToken: context.CancellationToken);
        var persistence = uow.GetPartyPersistence();
        var result = await persistence.UpsertParty(context.Message.Party, context.CancellationToken);
        result.EnsureSuccess();

        await uow.CommitAsync(context.CancellationToken);

        var partyUpdatedEvt = new PartyUpdatedEvent
        {
            PartyUuid = result.Value.PartyUuid.Value,
        };

        await context.Publish(partyUpdatedEvt, context.CancellationToken);
        _meters.PartiesUpserted.Add(1);
    }

    /// <summary>
    /// Meters for <see cref="PartyImportConsumer"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties upserted.
        /// </summary>
        public Counter<int> PartiesUpserted { get; }
            = telemetry.CreateCounter<int>("register.party-import.parties.upserted", "The number of parties upserted.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }
}
