#nullable enable

using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Contracts.Parties;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Utils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public sealed partial class A2PartyImportSaga
    : ISaga<A2PartyImportSaga, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaStartedBy<A2PartyImportSaga, ImportA2PartyCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaStartedBy<A2PartyImportSaga, ImportA2UserProfileCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaHandles<A2PartyImportSaga, CompleteA2PartyImportSagaCommand, A2PartyImportSaga.A2PartyImportSagaData>
{
    /// <inheritdoc/>
    public static string Name => nameof(A2PartyImportSaga);

    private readonly SagaContext<A2PartyImportSagaData> _context;
    private readonly IA2PartyImportService _importService;
    private readonly TimeProvider _timeProvider;
    private readonly IExternalRoleDefinitionPersistence _roleDefinitions;
    private readonly IPartyPersistence _parties;
    private readonly IPartyExternalRolePersistence _roles;
    private readonly IImportJobTracker _tracker;
    private readonly ILogger<A2PartyImportSaga> _logger;

    private A2PartyImportSagaData State => _context.State.Data!;

    private Guid SagaId => _context.SagaId;

    private void MarkComplete(bool error = false)
    {
        _context.State.Status = error ? SagaStatus.Faulted : SagaStatus.Completed;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportSaga"/> class.
    /// </summary>
    public A2PartyImportSaga(
        SagaContext<A2PartyImportSagaData> context,
        IA2PartyImportService importService,
        TimeProvider timeProvider,
        IExternalRoleDefinitionPersistence roleDefinitions,
        IPartyPersistence parties,
        IPartyExternalRolePersistence roles,
        IImportJobTracker tracker,
        ILogger<A2PartyImportSaga> logger)
    {
        _context = context;
        _importService = importService;
        _timeProvider = timeProvider;
        _roleDefinitions = roleDefinitions;
        _parties = parties;
        _roles = roles;
        _tracker = tracker;
        _logger = logger;
    }

    /// <inheritdoc/>
    public static ValueTask<A2PartyImportSagaData> CreateInitialState(IServiceProvider services, ImportA2PartyCommand command)
        => ValueTask.FromResult(new A2PartyImportSagaData
        {
            PartyUuid = command.PartyUuid,
            UserId = null, // we will only fetch latest user, not any specific one
            Tracking = command.Tracking,
        });

    /// <inheritdoc/>
    public static ValueTask<A2PartyImportSagaData> CreateInitialState(IServiceProvider services, ImportA2UserProfileCommand command)
        => ValueTask.FromResult(new A2PartyImportSagaData
        {
            PartyUuid = command.OwnerPartyUuid,
            UserId = command.UserId,
            Tracking = command.Tracking,
        });

    /// <inheritdoc/>
    public async Task Handle(ImportA2PartyCommand message, CancellationToken cancellationToken)
    {
        Debug.Assert(message.PartyUuid == State.PartyUuid);

        var now = _timeProvider.GetUtcNow();

        if (await FetchParty(cancellationToken) == FlowControl.Break)
        {
            return;
        }

        await Next(now, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task Handle(ImportA2UserProfileCommand message, CancellationToken cancellationToken)
    {
        Debug.Assert(message.OwnerPartyUuid == State.PartyUuid);

        var now = _timeProvider.GetUtcNow();

        var profileResult = await _importService.GetProfile(message.UserId, cancellationToken);
        if (profileResult is { Problem.ErrorCode: var errorCode }
            && errorCode == Problems.PartyGone.ErrorCode)
        {
            // Party is gone, so we can skip it. These should be rare, so don't bother with tracking.
            Log.ProfileGone(_logger, message.UserId);
            MarkComplete();
            return;
        }

        profileResult.EnsureSuccess();
        var profile = profileResult.Value;

        switch (profile.ProfileType)
        {
            case A2UserProfileType.Person when !profile.IsActive:
                await UpsertUserRecordAndComplete(profile, cancellationToken);
                return;

            case A2UserProfileType.Person:
            case A2UserProfileType.SelfIdentifiedUser:
                if (State.Party is null && await FetchParty(cancellationToken) == FlowControl.Break)
                {
                    return;
                }

                ApplyProfile(profile, now);
                break;

            case A2UserProfileType.EnterpriseUser:
                State.Party = MapEnterpriseProfile(profile, now);
                break;

            default:
                ThrowHelper.ThrowInvalidOperationException($"Unknown profile type '{profile.ProfileType}' for user ID {message.UserId}.");
                break;
        }

        await Next(now, cancellationToken);

        static EnterpriseUserRecord MapEnterpriseProfile(A2ProfileRecord profile, DateTimeOffset now)
        {
            if (!profile.UserUuid.HasValue)
            {
                ThrowHelper.ThrowInvalidOperationException($"Enterprise user profile for user ID {profile.UserId} is missing required UserUuid.");
            }

            if (string.IsNullOrEmpty(profile.UserName))
            {
                ThrowHelper.ThrowInvalidOperationException($"Enterprise user profile for user ID {profile.UserId} is missing required UserName.");
            }

            var isDeleted = !profile.IsActive;
            FieldValue<DateTimeOffset> deletedAt = FieldValue.Null;
            if (isDeleted)
            {
                deletedAt = profile.LastChangedAt ?? now;
            }

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
                IsDeleted = isDeleted,
                DeletedAt = deletedAt,
                VersionId = FieldValue.Unset,
            };

            return partyRecord;
        }
    }

    /// <inheritdoc/>
    public async Task Handle(CompleteA2PartyImportSagaCommand message, CancellationToken cancellationToken)
    {
        if (State.Party is null)
        {
            throw new InvalidOperationException("Party is not set");
        }

        PartyImportHelper.ValidatePartyForUpset(State.Party);
        var partyResult = await _parties.UpsertParty(State.Party, cancellationToken);
        partyResult.EnsureSuccess();

        await _context.Publish(
            new PartyUpdatedEvent
            {
                Party = partyResult.Value.PartyUuid.Value.ToPartyReferenceContract(),
            },
            cancellationToken);

        var fromParty = State.PartyUuid;
        List<Task> publishTasks = [];
        foreach (var (source, assignments) in State.RoleAssignments)
        {
            publishTasks.Clear();
            var upsertEvts = _roles.UpsertExternalRolesFromPartyBySource(
                commandId: SagaId,
                partyUuid: fromParty, 
                roleSource: source, 
                assignments: assignments.Select(ra => new IPartyExternalRolePersistence.UpsertExternalRoleAssignment
                {
                    RoleIdentifier = ra.Identifier,
                    ToParty = ra.ToPartyUuid,
                }),
                cancellationToken: cancellationToken);

            await foreach (var upsertEvt in upsertEvts.WithCancellation(cancellationToken))
            {
                var publishTask = upsertEvt.Type switch
                {
                    ExternalRoleAssignmentEvent.EventType.Added => _context.Publish(
                        new ExternalRoleAssignmentAddedEvent
                        {
                            VersionId = upsertEvt.VersionId,
                            Role = upsertEvt.ToPartyExternalRoleReferenceContract(),
                            From = upsertEvt.FromParty.ToPartyReferenceContract(),
                            To = upsertEvt.ToParty.ToPartyReferenceContract(),
                        },
                        cancellationToken),

                    ExternalRoleAssignmentEvent.EventType.Removed => _context.Publish(
                        new ExternalRoleAssignmentRemovedEvent
                        {
                            VersionId = upsertEvt.VersionId,
                            Role = upsertEvt.ToPartyExternalRoleReferenceContract(),
                            From = upsertEvt.FromParty.ToPartyReferenceContract(),
                            To = upsertEvt.ToParty.ToPartyReferenceContract(),
                        },
                        cancellationToken),

                    _ => ThrowHelper.ThrowInvalidOperationException<Task>($"The event type '{upsertEvt.Type}' is not supported."),
                };

                publishTasks.Add(publishTask);
            }

            await Task.WhenAll(publishTasks);
        }

        if (State.Tracking.HasValue)
        {
            await _tracker.TrackProcessedStatus(State.Tracking.JobName, new ImportJobProcessingStatus { ProcessedMax = State.Tracking.Progress }, cancellationToken);
        }

        MarkComplete();
    }

    private async Task Next(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // TODO: Split up to separate messages?
        Debug.Assert(State.Party is not null);
        if (State.Party.PartyType.Value is PartyRecordType.Person or PartyRecordType.SelfIdentifiedUser && State.Party.User.IsUnset)
        {
            Result<A2ProfileRecord> userRecordResult;
            if (State.Party.PartyType.Value is PartyRecordType.Person)
            {
                userRecordResult = await _importService.GetOrCreatePersonUser(State.PartyUuid, cancellationToken);
            }
            else
            {
                userRecordResult = await _importService.GetPartyUser(State.PartyUuid, cancellationToken);
            }

            userRecordResult.EnsureSuccess();
            ApplyProfile(userRecordResult.Value, now);
        }

        if (State.Party.PartyType.Value is PartyRecordType.Organization && !State.RoleAssignments.ContainsKey(ExternalRoleSource.CentralCoordinatingRegister))
        {
            if (await FetchCcrRoleAssignments(cancellationToken) == FlowControl.Break)
            {
                MarkComplete();
                return;
            }
        }

        await _context.Send(
            new CompleteA2PartyImportSagaCommand
            {
                CorrelationId = SagaId,
            },
            cancellationToken);
    }

    private void ApplyProfile(A2ProfileRecord profile, DateTimeOffset now)
    {
        State.Party = State.Party! with
        {
            User = new PartyUserRecord(profile.UserId, profile.UserName),
        };

        if (profile.ProfileType is A2UserProfileType.SelfIdentifiedUser && !State.Party.IsDeleted.Value && !profile.IsActive)
        {
            State.Party = State.Party with
            {
                IsDeleted = true,
                DeletedAt = profile.LastChangedAt ?? now,
            };
        }
    }

    private async Task UpsertUserRecordAndComplete(A2ProfileRecord profile, CancellationToken cancellationToken)
    {
        await _context.Send(
            new UpsertUserRecordCommand
            {
                PartyUuid = profile.PartyUuid,
                UserId = profile.UserId,
                Username = profile.UserName,

                // Note: The `IsActive` field is recently added to A2, so will be null for older versions of A2. The `IsDeleted` field
                // on the message is the opposite of `IsActive`, but get's stored whenever a user is modified, so may be out of date.
                IsActive = profile.IsActive,
                Tracking = State.Tracking,
            },
            cancellationToken);

        MarkComplete();
    }

    private async Task<FlowControl> FetchParty(CancellationToken cancellationToken)
    {
        var partyResult = await _importService.GetParty(State.PartyUuid, cancellationToken);
        if (partyResult is { Problem.ErrorCode: var errorCode }
            && errorCode == Problems.PartyGone.ErrorCode)
        {
            // Party is gone, so we can skip it. These should be rare, so don't bother with tracking.
            Log.PartyGone(_logger, State.PartyUuid);
            MarkComplete();
            return FlowControl.Break;
        }

        partyResult.EnsureSuccess();
        State.Party = partyResult.Value;
        return FlowControl.Continue;
    }

    private async Task<FlowControl> FetchCcrRoleAssignments(CancellationToken cancellationToken)
    {
        var assignments = await _importService.GetExternalRoleAssignmentsFrom(State.Party!.PartyId.Value, State.PartyUuid, cancellationToken)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            State.RoleAssignments.Add(ExternalRoleSource.CentralCoordinatingRegister, []);
            return FlowControl.Continue;
        }

        var mapped = new List<UpsertExternalRoleAssignmentsCommand.Assignment>(assignments.Count);
        foreach (var assignment in assignments)
        {
            var roleDefinition = await _roleDefinitions.TryGetRoleDefinitionByRoleCode(assignment.RoleCode, cancellationToken);
            if (roleDefinition is null)
            {
                Log.RoleWithRoleCodeNotFound(_logger, assignment.RoleCode, State.PartyUuid, assignment.ToPartyUuid);
                continue;
            }

            if (roleDefinition.Source != ExternalRoleSource.CentralCoordinatingRegister)
            {
                Log.RoleWithWrongSource(_logger, assignment.RoleCode, State.PartyUuid, assignment.ToPartyUuid, roleDefinition.Source, roleDefinition.Identifier);
                continue;
            }

            mapped.Add(new()
            {
                Identifier = roleDefinition.Identifier,
                ToPartyUuid = assignment.ToPartyUuid,
            });
        }

        State.RoleAssignments.Add(ExternalRoleSource.CentralCoordinatingRegister, mapped);
        return FlowControl.Continue;
    }

    /// <summary>
    /// State data for <see cref="A2PartyImportSaga"/>.
    /// </summary>
    public sealed class A2PartyImportSagaData
        : ISagaStateData<A2PartyImportSagaData>
    {
        /// <inheritdoc/>
        public static string StateType => nameof(A2PartyImportSagaData);

        /// <summary>
        /// Gets the unique identifier for the party.
        /// </summary>
        public required Guid PartyUuid { get; init; }

        /// <summary>
        /// Gets the unique identifier of the user associated with this instance.
        /// </summary>
        public required ulong? UserId { get; init; }

        /// <summary>
        /// Gets tracking information for the import job.
        /// </summary>
        public required UpsertPartyTracking Tracking { get; init; }

        /// <summary>
        /// Gets or sets the party being upserted.
        /// </summary>
        public PartyRecord? Party { get; set; }

        /// <summary>
        /// Gets or sets the collection of role assignments grouped by external role source.
        /// </summary>
        public Dictionary<ExternalRoleSource, IReadOnlyList<UpsertExternalRoleAssignmentsCommand.Assignment>> RoleAssignments { get; set; } = new();
    }

    private enum FlowControl
    {
        Continue,
        Break,
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Party with UUID {PartyUuid} is gone.")]
        public static partial void PartyGone(ILogger logger, Guid partyUuid);

        [LoggerMessage(1, LogLevel.Information, "User with ID {UserId} is gone.")]
        public static partial void ProfileGone(ILogger logger, ulong userId);

        [LoggerMessage(2, LogLevel.Warning, "External role-definition with role code '{RoleCode}' not found. Attempted to add from party '{FromPartyUuid}' to party '{ToPartyUuid}'.")]
        public static partial void RoleWithRoleCodeNotFound(ILogger logger, string roleCode, Guid fromPartyUuid, Guid toPartyUuid);

        [LoggerMessage(3, LogLevel.Warning, "External role-definition with role code '{RoleCode}' found, but with the wrong source '{Source}' and identifier '{Identifier}'. Attempted to add from party '{FromPartyUuid}' to party '{ToPartyUuid}'.")]
        public static partial void RoleWithWrongSource(ILogger logger, string roleCode, Guid fromPartyUuid, Guid toPartyUuid, ExternalRoleSource source, string identifier);
    }
}
