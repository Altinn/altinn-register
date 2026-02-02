#nullable enable

using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;
using Altinn.Register.Core;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.PartyImport.Npr;
using Altinn.Urn;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public sealed partial class A2PartyImportSaga
    : ISaga<A2PartyImportSaga, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaStartedBy<A2PartyImportSaga, ImportA2PartyCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaStartedBy<A2PartyImportSaga, ImportA2UserProfileCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaHandles<A2PartyImportSaga, CompleteA2PartyImportSagaCommand, A2PartyImportSaga.A2PartyImportSagaData>
    , ISagaHandles<A2PartyImportSaga, RetryA2PartyImportSagaCommand, A2PartyImportSaga.A2PartyImportSagaData>
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
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
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
        IConfiguration configuration,
        IServiceProvider services,
        ILogger<A2PartyImportSaga> logger)
    {
        _context = context;
        _importService = importService;
        _timeProvider = timeProvider;
        _roleDefinitions = roleDefinitions;
        _parties = parties;
        _roles = roles;
        _tracker = tracker;
        _configuration = configuration;
        _services = services;
        _logger = logger;
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

        if (_configuration.GetValue("Altinn:register:PartyImport:Npr:Guardianships:Enable", defaultValue: false) && State.Party.PartyType.Value is PartyRecordType.Person && !State.RoleAssignments.ContainsKey(ExternalRoleSource.CivilRightsAuthority))
        {
            if (await FetchCraRoleAssignments(cancellationToken) == FlowControl.Break)
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

        if (profile.ProfileType is A2UserProfileType.SelfIdentifiedUser)
        {
            Debug.Assert(State.Party is SelfIdentifiedUserRecord);
            var si = (SelfIdentifiedUserRecord)State.Party;

            if (!si.IsDeleted.Value && !profile.IsActive)
            {
                si = si with
                {
                    IsDeleted = true,
                    DeletedAt = profile.LastChangedAt ?? now,
                };
            }

            Debug.Assert(profile.ExternalAuthenticationReference != string.Empty);
            si = profile.ExternalAuthenticationReference switch
            {
                null => ApplyLegacyProfile(si, profile),
                string s when PartyExternalRefUrn.TryParse(s, out var urn) && urn.IsIDPortenEmail(out var email) => ApplyEpostProfile(si, email.Value),
                _ => ApplyEducationalProfile(si),
            };

            State.Party = si;
        }

        static SelfIdentifiedUserRecord ApplyLegacyProfile(SelfIdentifiedUserRecord si, A2ProfileRecord profile)
        {
            Debug.Assert(!string.IsNullOrEmpty(profile.UserName));
            return si with
            {
                SelfIdentifiedUserType = SelfIdentifiedUserType.Legacy,
                Email = FieldValue.Null,
                ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create(profile.UserName.ToLowerInvariant())),
            };
        }

        static SelfIdentifiedUserRecord ApplyEducationalProfile(SelfIdentifiedUserRecord si)
        {
            return si with
            {
                SelfIdentifiedUserType = SelfIdentifiedUserType.Educational,
                Email = FieldValue.Null,
            };
        }

        static SelfIdentifiedUserRecord ApplyEpostProfile(SelfIdentifiedUserRecord si, string email)
        {
            email = email.ToLowerInvariant();

            return si with
            {
                SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
                Email = email,
                ExternalUrn = PartyExternalRefUrn.IDPortenEmail.Create(UrnEncoded.Create(email)),
                DisplayName = email,
            };
        }
    }

    private async Task<FlowControl> FetchParty(CancellationToken cancellationToken)
    {
        using var activity = RegisterTelemetry.StartActivity("fetch party altinn 2", ActivityKind.Internal, tags: [new("party.uuid", State.PartyUuid)]);

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
        Debug.Assert(State.Party is { PartyType.HasValue: true, PartyType.Value: PartyRecordType.Organization });
        using var activity = RegisterTelemetry.StartActivity("fetch ccr roles from altinn 2", ActivityKind.Internal, tags: [new("party.uuid", State.PartyUuid)]);

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

    private async Task<FlowControl> FetchCraRoleAssignments(CancellationToken cancellationToken)
    {
        Debug.Assert(State.Party is { PartyType.HasValue: true, PartyType.Value: PartyRecordType.Person });
        Debug.Assert(State.Party is { PersonIdentifier.HasValue: true });
        using var activity = RegisterTelemetry.StartActivity("fetch guardianships from npr", ActivityKind.Internal, tags: [new("party.uuid", State.PartyUuid)]);

        var client = _services.GetRequiredService<NprClient>();
        var result = await client.GetGuardianshipsForPerson(State.Party.PersonIdentifier.Value, cancellationToken);
        if (result.IsProblem && result.Problem.ErrorCode == Problems.PartyNotFound.ErrorCode)
        {
            // All environments contains persons that are not in NPR. These can be skipped.
            return FlowControl.Continue;
        }

        result.EnsureSuccess();

        var guardianships = result.Value;
        var roleCount = guardianships.Sum(static g => g.Roles.Count);

        var guardians = await _parties.LookupParties(
            personIdentifiers: [.. guardianships.Select(static g => g.Guardian)],
            include: PartyFieldIncludes.PartyUuid | PartyFieldIncludes.PartyPersonIdentifier,
            cancellationToken: cancellationToken)
            .ToDictionaryAsync(
                keySelector: static p => p.PersonIdentifier.Value!,
                elementSelector: static p => p.PartyUuid.Value,
                cancellationToken: cancellationToken);

        var mapped = new List<UpsertExternalRoleAssignmentsCommand.Assignment>(roleCount);
        foreach (var guardianship in guardianships)
        {
            if (!guardians.TryGetValue(guardianship.Guardian, out var guardianUuid))
            {
                throw new InvalidOperationException($"Guardian not found in party database.");
            }

            foreach (var role in guardianship.Roles)
            {
                mapped.Add(new()
                {
                    Identifier = role,
                    ToPartyUuid = guardianUuid,
                });
            }
        }

        State.RoleAssignments.Add(ExternalRoleSource.CivilRightsAuthority, mapped);
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

        /// <summary>
        /// Clears non-initial state data.
        /// </summary>
        internal void Clear()
        {
            Party = null;
            RoleAssignments.Clear();
        }
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
