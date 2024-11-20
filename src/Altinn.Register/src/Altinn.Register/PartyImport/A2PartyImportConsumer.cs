using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Core.UnitOfWork;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for importing parties from A2.
/// </summary>
public sealed partial class A2PartyImportConsumer
    : IConsumer<Batch<ImportA2PartyCommand>>
{
    private readonly IA2PartyImportService _importService;
    private readonly IUnitOfWorkManager _uowManager;
    private readonly ILogger<A2PartyImportConsumer> _logger;

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<Batch<ImportA2PartyCommand>> context)
    {
        var parties = await Task.WhenAll(context.Message.Select(msg => GetParty(msg.Message, context.CancellationToken)));

        await using var uow = await _uowManager.CreateAsync(
            activityName: "batch import A2 parties",
            tags: [
                new("batch.size", parties.Length),
            ]);

        var persistence = uow.GetPartyPersistence();
        foreach (var party in parties)
        {
            await persistence.UpsertParty(party, context.CancellationToken);
            Log.ImportedParty(_logger, party.PartyUuid.Value);
        }

        await uow.CommitAsync(context.CancellationToken);
    }

    private Task<PartyRecord> GetParty(ImportA2PartyCommand command, CancellationToken cancellationToken)
        => _importService.GetParty(command.PartyUuid, cancellationToken);

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Imported party {PartyUuid}.")]
        public static partial void ImportedParty(ILogger logger, Guid partyUuid);
    }
}
