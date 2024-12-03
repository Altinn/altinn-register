#nullable enable

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Platform.Register.Models;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Core.UnitOfWork;
using MassTransit;

namespace Altinn.Register.PartyImport;

/// <summary>
/// Consumer for importing parties from A2.
/// </summary>
public sealed partial class A2PartyImportConsumer
    : IConsumer<ImportA2PartyBatchCommand>
    , IConsumer<Fault<ImportA2PartyBatchCommand>>
    , IConsumer<RetryImportSingleA2PartyCommand>
    , IConsumer<Fault<RetryImportSingleA2PartyCommand>>
{
    private readonly A2PartyImportMeters _meters;
    private readonly ICommandSender _commandSender;
    private readonly IImportJobTracker _tracker;
    private readonly IA2PartyImportService _importService;
    private readonly IUnitOfWorkManager _uowManager;
    private readonly ILogger<A2PartyImportConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportConsumer"/> class.
    /// </summary>
    public A2PartyImportConsumer(
        A2PartyImportMeters meters,
        ICommandSender commandSender,
        IImportJobTracker tracker,
        IA2PartyImportService importService,
        IUnitOfWorkManager uowManager,
        ILogger<A2PartyImportConsumer> logger)
    {
        _meters = meters;
        _commandSender = commandSender;
        _tracker = tracker;
        _importService = importService;
        _uowManager = uowManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2PartyBatchCommand> context)
    {
        var source = Activity.Current;
        Activity.Current = null;

        await using var uow = await _uowManager.CreateAsync(
            activityName: "batch import A2 parties",
            tags: [
                new("batch.size", context.Message.Items.Count),
            ],
            links: source is null ? [] : [new(source.Context)]);

        List<PartyRecord> parties = new(context.Message.Items.Count);
        
        {
            using var activity = RegisterTelemetry.StartActivity("fetch A2 parties", ActivityKind.Internal);
            foreach (var item in context.Message.Items)
            {
                var party = await GetParty(item.PartyUuid, context.CancellationToken);
                parties.Add(party);
            }
        }

        {
            using var activity = RegisterTelemetry.StartActivity("upsert parties", ActivityKind.Internal);
            var persistence = uow.GetPartyPersistence();
            foreach (var party in parties)
            {
                await UpsertParty(persistence, party, party.PartyUuid.Value, context.CancellationToken);
                Log.ImportedParty(_logger, party.PartyUuid.Value);
            }
        }

        {
            using var activity = RegisterTelemetry.StartActivity("commit transaction", ActivityKind.Internal);
            await uow.CommitAsync(context.CancellationToken);
        }

        {
            using var activity = RegisterTelemetry.StartActivity("track batch import", ActivityKind.Internal);
            var maxChangeId = context.Message.Items.Max(static i => i.ChangeId);

            await _tracker.TrackProcessedStatus(JobNames.A2PartyImportParty, new ImportJobProcessingStatus { ProcessedMax = maxChangeId }, context.CancellationToken);
        }

        _meters.PartiesImported.Add(parties.Count);
        _meters.BatchesSucceeded.Add(1);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<RetryImportSingleA2PartyCommand> context)
    {
        var source = Activity.Current;
        Activity.Current = null;

        await using var uow = await _uowManager.CreateAsync(
            activityName: "import A2 party",
            tags: [
                new("party.uuid", context.Message.PartyUuid),
            ],
            links: source is null ? [] : [new(source.Context)]);

        PartyRecord party;

        {
            using var activity = RegisterTelemetry.StartActivity("fetch A2 parties", ActivityKind.Internal);
            party = await GetParty(context.Message.PartyUuid, context.CancellationToken);
        }

        {
            using var activity = RegisterTelemetry.StartActivity("upsert parties", ActivityKind.Internal);
            var persistence = uow.GetPartyPersistence();
            await UpsertParty(persistence, party, context.Message.PartyUuid, context.CancellationToken);
            Log.ImportedParty(_logger, party.PartyUuid.Value);
        }

        {
            using var activity = RegisterTelemetry.StartActivity("commit transaction", ActivityKind.Internal);
            await uow.CommitAsync(context.CancellationToken);
        }

        {
            using var activity = RegisterTelemetry.StartActivity("track batch import", ActivityKind.Internal);
            var maxChangeId = context.Message.ChangeId;

            await _tracker.TrackProcessedStatus(JobNames.A2PartyImportParty, new ImportJobProcessingStatus { ProcessedMax = maxChangeId }, context.CancellationToken);
        }

        _meters.PartiesImported.Add(1);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<Fault<ImportA2PartyBatchCommand>> context)
    {
        // we have a faulted batch, split up the batch and retry each message individually
        foreach (var item in context.Message.Message.Items)
        {
            var cmd = RetryImportSingleA2PartyCommand.MapFrom(item);
            await _commandSender.Send(cmd, context.CancellationToken);
        }

        _meters.BatchesFailed.Add(1);
    }

    /// <inheritdoc />
    public Task Consume(ConsumeContext<Fault<RetryImportSingleA2PartyCommand>> context)
    {
        _meters.PartiesImportsFailed.Add(1);

        return Task.CompletedTask;
    }

    private async Task<PartyRecord> GetParty(Guid partyUuid, CancellationToken cancellationToken)
    {
        using var activity = RegisterTelemetry.StartActivity("fetch A2 party", ActivityKind.Internal, tags: [new("party.uuid", partyUuid)]);

        return await _importService.GetParty(partyUuid, cancellationToken); 
    }

    private async Task UpsertParty(IPartyPersistence persistence, PartyRecord party, Guid partyUuid, CancellationToken cancellationToken)
    {
        Assert(party.PartyUuid.HasValue, "party must have PartyUuid set", partyUuid);
        Assert(party.PartyId.HasValue, "party must have PartyId set", partyUuid);
        Assert(party.PartyType.HasValue, "party must have PartyType set", partyUuid);
        Assert(party.Name.HasValue, "party must have Name set", partyUuid);
        Assert(party.PersonIdentifier.IsSet, "party must have PersonIdentifier set", partyUuid);
        Assert(party.OrganizationIdentifier.IsSet, "party must have OrganizationIdentifier set", partyUuid);
        Assert(party.CreatedAt.HasValue, "party must have CreatedAt set", partyUuid);
        Assert(party.ModifiedAt.HasValue, "party must have ModifiedAt set", partyUuid);

        if (party is PersonRecord person)
        {
            Assert(person.FirstName.HasValue, "person must have FirstName set", partyUuid);
            Assert(person.MiddleName.IsSet, "person must have MiddleName set", partyUuid);
            Assert(person.LastName.HasValue, "person must have LastName set", partyUuid);
            Assert(person.Address.IsSet, "person must have Address set", partyUuid);
            Assert(person.MailingAddress.IsSet, "person must have MailingAddress set", partyUuid);
            Assert(person.DateOfBirth.HasValue, "person must have DateOfBirth set", partyUuid);
            Assert(person.DateOfDeath.IsSet, "person must have DateOfDeath set", partyUuid);
        }
        else if (party is OrganizationRecord org)
        {
            Assert(org.UnitStatus.HasValue, "organization must have UnitStatus set", partyUuid);
            Assert(org.UnitType.HasValue, "organization must have UnitType set", partyUuid);
            Assert(org.TelephoneNumber.IsSet, "organization must have TelephoneNumber set", partyUuid);
            Assert(org.MobileNumber.IsSet, "organization must have MobileNumber set", partyUuid);
            Assert(org.FaxNumber.IsSet, "organization must have FaxNumber set", partyUuid);
            Assert(org.EmailAddress.IsSet, "organization must have EmailAddress set", partyUuid);
            Assert(org.InternetAddress.IsSet, "organization must have InternetAddress set", partyUuid);
            Assert(org.MailingAddress.IsSet, "organization must have MailingAddress set", partyUuid);
            Assert(org.BusinessAddress.IsSet, "organization must have BusinessAddress set", partyUuid);
        }

        await persistence.UpsertParty(party, cancellationToken);
    }

    private static void Assert(bool condition, string message, Guid partyUuid)
    {
        if (!condition)
        {
            throw new InvalidPartyException($"Party {partyUuid} failed validation: {message}");
        }
    }

    private sealed class InvalidPartyException(string message)
        : InvalidOperationException(message)
    {
    }

    /// <summary>
    /// Meters for <see cref="A2PartyImportConsumer"/>.
    /// </summary>
    /// <remarks>This should be registered as a singleton.</remarks>
    public sealed class A2PartyImportMeters(RegisterTelemetry telemetry)
    {
        /// <summary>
        /// Gets a counter for the number of parties imported from A2.
        /// </summary>
        public Counter<int> PartiesImported { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.parties.imported", "The number of parties imported from A2.");

        /// <summary>
        /// Gets a counter for the number of parties that have failed to import from A2.
        /// </summary>
        public Counter<int> PartiesImportsFailed { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.parties.failed", "The number of parties that have failed to import from A2.");

        /// <summary>
        /// Gets a counter for the number of parties that failed to import from A2.
        /// </summary>
        public Counter<int> BatchesSucceeded { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.batches.succeeded", "The number of batches that have been successfully imported from A2.");

        /// <summary>
        /// Gets a counter for the number of batches that have failed to import from A2.
        /// </summary>
        public Counter<int> BatchesFailed { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.batches.failed", "The number of batches that have failed to import from A2.");
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Imported party {PartyUuid}.")]
        public static partial void ImportedParty(ILogger logger, Guid partyUuid);
    }
}
