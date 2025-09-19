#nullable enable

using System.Diagnostics.Metrics;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Core;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using CommunityToolkit.Diagnostics;
using MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Consumer for importing user profiles from A2.
/// </summary>
public sealed partial class A2ProfileImportConsumer
    : IConsumer<ImportA2UserProfileCommand>
{
    private readonly IA2PartyImportService _importService;
    private readonly ICommandSender _sender;
    private readonly TimeProvider _timeProvider;
    private readonly ImportMeters _meters;
    private readonly ILogger<A2ProfileImportConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportConsumer"/> class.
    /// </summary>
    public A2ProfileImportConsumer(
        ILogger<A2ProfileImportConsumer> logger,
        IA2PartyImportService importService,
        ICommandSender commandSender,
        TimeProvider timeProvider,
        RegisterTelemetry telemetry)
    {
        _logger = logger;
        _importService = importService;
        _sender = commandSender;
        _timeProvider = timeProvider;
        _meters = telemetry.GetServiceMeters<ImportMeters>();
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ImportA2UserProfileCommand> context)
    {
        var profileResult = await _importService.GetProfile(context.Message.UserId, context.CancellationToken);
        if (profileResult is { Problem.ErrorCode: var errorCode }
            && errorCode == Problems.PartyGone.ErrorCode)
        {
            // Profile is gone, so we can skip it. These should be rare, so don't bother with tracking.
            Log.ProfileGone(_logger, context.Message.UserId);
            return;
        }

        profileResult.EnsureSuccess();
        var profile = profileResult.Value;

        switch (profile.ProfileType)
        {
            case A2UserProfileType.Person:
                await UpsertPersonOwnedUser(profile, context.Message, context.CancellationToken);
                break;

            case A2UserProfileType.SelfIdentifiedUser:
                await UpsertSelfIdentifiedParty(profile, context.Message, context.CancellationToken);
                break;

            case A2UserProfileType.EnterpriseUser:
                await UpsertEnterpriseParty(profile, context.Message, context.CancellationToken);
                break;

            default:
                ThrowHelper.ThrowInvalidOperationException($"Unknown profile type '{profile.ProfileType}' for user ID {context.Message.UserId}.");
                break;
        }

        _meters.PartiesFetched.Add(1, [new("profile.type", ProfileTypeTag(profile.ProfileType))]);
    }

    private async Task UpsertPersonOwnedUser(A2ProfileRecord profile, ImportA2UserProfileCommand message, CancellationToken cancellationToken)
    {
        await _sender.Send(
            new UpsertUserRecordCommand
            {
                PartyUuid = profile.PartyUuid,
                UserId = profile.UserId,
                Username = profile.UserName,

                // Note: The `IsActive` field is recently added to A2, so will be null for older versions of A2. The `IsDeleted` field
                // on the message is the opposite of `IsActive`, but get's stored whenever a user is modified, so may be out of date.
                IsActive = profile.IsActive ?? !message.IsDeleted,
                Tracking = message.Tracking,
            },
            cancellationToken);
    }

    private async Task UpsertSelfIdentifiedParty(A2ProfileRecord profile, ImportA2UserProfileCommand message, CancellationToken cancellationToken)
    {
        if (!profile.UserUuid.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException($"Self identified user profile for user ID {profile.UserId} is missing required UserUuid.");
        }

        if (string.IsNullOrEmpty(profile.UserName))
        {
            ThrowHelper.ThrowInvalidOperationException($"Self identified user profile for user ID {profile.UserId} is missing required UserName.");
        }

        var now = _timeProvider.GetUtcNow();
        var partyRecord = new SelfIdentifiedUserRecord
        {
            PartyUuid = profile.UserUuid.Value,
            OwnerUuid = FieldValue.Null,
            PartyId = profile.PartyId,
            DisplayName = profile.UserName,
            PersonIdentifier = FieldValue.Null,
            OrganizationIdentifier = FieldValue.Null,
            CreatedAt = now,
            ModifiedAt = now,
            User = new PartyUserRecord(profile.UserId, profile.UserName, ImmutableValueArray.Create(profile.UserId)),
            IsDeleted = !(profile.IsActive ?? !message.IsDeleted),
            VersionId = FieldValue.Unset,
        };

        await _sender.Send(
            new UpsertPartyCommand
            {
                Party = partyRecord,
                Tracking = message.Tracking,
            },
            cancellationToken);
    }

    private async Task UpsertEnterpriseParty(A2ProfileRecord profile, ImportA2UserProfileCommand message, CancellationToken cancellationToken)
    {
        if (!profile.UserUuid.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException($"Enterprise user profile for user ID {profile.UserId} is missing required UserUuid.");
        }

        if (string.IsNullOrEmpty(profile.UserName))
        {
            ThrowHelper.ThrowInvalidOperationException($"Enterprise user profile for user ID {profile.UserId} is missing required UserName.");
        }

        var now = _timeProvider.GetUtcNow();
        var partyRecord = new EnterpriseUserRecord
        {
            PartyUuid = profile.UserUuid.Value,
            OwnerUuid = profile.PartyUuid,
            PartyId = FieldValue.Null,
            DisplayName = profile.UserName,
            PersonIdentifier = FieldValue.Null,
            OrganizationIdentifier = FieldValue.Null,
            CreatedAt = now,
            ModifiedAt = now,
            User = new PartyUserRecord(profile.UserId, profile.UserName, ImmutableValueArray.Create(profile.UserId)),
            IsDeleted = !(profile.IsActive ?? !message.IsDeleted),
            VersionId = FieldValue.Unset,
        };

        await _sender.Send(
            new UpsertPartyCommand
            {
                Party = partyRecord,
                Tracking = message.Tracking,
            },
            cancellationToken);
    }

    private static string? ProfileTypeTag(A2UserProfileType profileType)
        => profileType switch
        {
            A2UserProfileType.Person => "person",
            A2UserProfileType.SelfIdentifiedUser => "self-identified",
            A2UserProfileType.EnterpriseUser => "enterprise",
            _ => null,
        };

    /// <summary>
    /// Meters for <see cref="A2ProfileImportConsumer"/>.
    /// </summary>
    private sealed class ImportMeters(RegisterTelemetry telemetry)
        : IServiceMeters<ImportMeters>
    {
        /// <summary>
        /// Gets a counter for the number of parties imported from A2.
        /// </summary>
        public Counter<int> PartiesFetched { get; }
            = telemetry.CreateCounter<int>("register.party-import.a2.profiles.fetched", "The number of profiles fetched from A2.");

        /// <inheritdoc/>
        public static ImportMeters Create(RegisterTelemetry telemetry)
            => new ImportMeters(telemetry);
    }

    private static partial class Log 
    {
        [LoggerMessage(0, LogLevel.Information, "User with ID {UserId} is gone.")]
        public static partial void ProfileGone(ILogger logger, ulong userId);
    }

    /// <summary>
    /// Consumer definition for <see cref="A2ProfileImportConsumer"/>.
    /// </summary>
    public sealed class Definition
        : ConsumerDefinition<A2PartyImportConsumer>
    {
        /// <inheritdoc/>
        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<A2PartyImportConsumer> consumerConfigurator,
            IRegistrationContext context)
        {
            base.ConfigureConsumer(endpointConfigurator, consumerConfigurator, context);

            consumerConfigurator.UseConcurrentMessageLimit(10, endpointConfigurator);
        }
    }
}
