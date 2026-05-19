using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts;
using Altinn.Register.Core.A2.SblProfile;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.PartyImport.A2;
using Altinn.Urn;
using Microsoft.Extensions.Logging;

namespace Altinn.Register.Core.Operations;

#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Request to get an existing self-identified user, or create one if none exists.
/// </summary>
/// <param name="SelfIdentifiedUserType">
/// The self-identified user type. Drives the response <c>ExternalUrn</c> shape; not used for identity construction.
/// </param>
/// <param name="ExternalIdentity">
/// The bridge-shape external identity (pre-built by the caller). Required.
/// </param>
/// <param name="UserName">
/// The username to assign on create. Required. The caller owns username generation;
/// register does not transform it.
/// </param>
/// <param name="Email">
/// The user's email. Required for <see cref="Contracts.SelfIdentifiedUserType.IdPortenEmail"/>;
/// ignored otherwise.
/// </param>
public readonly record struct GetOrCreateSelfIdentifiedUserRequest(
    SelfIdentifiedUserType SelfIdentifiedUserType,
    string? ExternalIdentity,
    string? UserName,
    string? Email)
    : IRequest<SelfIdentifiedUserRecord>;

/// <summary>
/// Get-or-create self-identified user via SBL Bridge proxy (iteration 1).
/// </summary>
/// <remarks>
/// Two-call flow: lookup by external identity first, create if not found. The bridge owns
/// UserId / PartyId / PartyUuid allocation during the proxy phase.
/// </remarks>
internal sealed partial class GetOrCreateSelfIdentifiedUserFromBridgeHandler(
    ISblProfileBridgeClient bridgeClient,
    ICommandSender commandSender,
    TimeProvider timeProvider,
    ILogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler> logger)
    : IRequestHandler<GetOrCreateSelfIdentifiedUserRequest, SelfIdentifiedUserRecord>
{
    private const int SbiUserTypeSelfIdentified = 2;

    /// <inheritdoc/>
    public async ValueTask<Result<SelfIdentifiedUserRecord>> Handle(
        GetOrCreateSelfIdentifiedUserRequest request,
        CancellationToken cancellationToken)
    {
        ValidationProblemBuilder builder = default;

        if (string.IsNullOrWhiteSpace(request.ExternalIdentity))
        {
            builder.Add(StdValidationErrors.Required, "/externalIdentity");
        }

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            builder.Add(StdValidationErrors.Required, "/userName");
        }

        if (request.SelfIdentifiedUserType == SelfIdentifiedUserType.IdPortenEmail
            && string.IsNullOrWhiteSpace(request.Email))
        {
            builder.Add(StdValidationErrors.Required, "/email");
        }

        if (builder.TryBuild(out var error))
        {
            return error;
        }

        var lookupResult = await bridgeClient.LookupUser(request.ExternalIdentity!, cancellationToken);
        if (lookupResult.IsProblem)
        {
            return lookupResult.Problem;
        }

        if (lookupResult.Value.Found)
        {
            return MapToRecord(lookupResult.Value.Profile, request);
        }

        var createRequest = new SblUserProfile
        {
            ExternalIdentity = request.ExternalIdentity,
            UserName = request.UserName,
            UserType = SbiUserTypeSelfIdentified,
        };

        var createResult = await bridgeClient.CreateUser(createRequest, cancellationToken);
        if (createResult.IsProblem)
        {
            return createResult.Problem;
        }

        var mapped = MapToRecord(createResult.Value, request);
        if (mapped.IsProblem)
        {
            return mapped.Problem;
        }

        await EnqueueLocalImport(mapped.Value.PartyUuid.Value, cancellationToken);
        return mapped;
    }

    // The polling A2PartyImportJob will eventually pick up this party as a change
    // from SBL, but we enqueue the same command eagerly so authentication can read
    // the party from local register on the immediate next request. ChangeId is 0 and
    // Tracking is left default — the polling job will track ProcessedMax when it
    // re-processes the same change.
    private async ValueTask EnqueueLocalImport(Guid partyUuid, CancellationToken cancellationToken)
    {
        try
        {
            await commandSender.Send(
                new ImportA2PartyCommand
                {
                    PartyUuid = partyUuid,
                    ChangeId = 0,
                    ChangedTime = timeProvider.GetUtcNow(),
                    Tracking = default,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Don't fail the create on a bus hiccup — polling job will reconcile.
            Log.EnqueueImportFailed(logger, ex, partyUuid);
        }
    }

    private Result<SelfIdentifiedUserRecord> MapToRecord(SblUserProfile profile, GetOrCreateSelfIdentifiedUserRequest request)
    {
        if (profile.UserUuid is null || profile.UserId <= 0 || profile.PartyId <= 0 || string.IsNullOrEmpty(profile.UserName))
        {
            Log.IncompleteBridgeResponse(logger);
            return Problems.PartyFetchFailed.Create([
                new("reason", "bridge response missing required identifiers"),
            ]);
        }

        FieldValue<PartyExternalRefUrn> externalUrn = request.SelfIdentifiedUserType switch
        {
            SelfIdentifiedUserType.IdPortenEmail
                => PartyExternalRefUrn.IDPortenEmail.Create(UrnEncoded.Create(request.Email!)),
            SelfIdentifiedUserType.Legacy
                => PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create(profile.UserName.ToLowerInvariant())),
            SelfIdentifiedUserType.Educational => FieldValue.Null,
            _ => FieldValue.Null,
        };

        FieldValue<string> email = request.SelfIdentifiedUserType == SelfIdentifiedUserType.IdPortenEmail
            ? request.Email!
            : FieldValue.Null;

        FieldValue<string> extRef = request.SelfIdentifiedUserType == SelfIdentifiedUserType.Educational
            ? request.ExternalIdentity!
            : FieldValue.Null;

        var userId = checked((uint)profile.UserId);
        var now = timeProvider.GetUtcNow();

        var displayName = request.SelfIdentifiedUserType == SelfIdentifiedUserType.IdPortenEmail
            ? request.Email!
            : profile.UserName;

        return new SelfIdentifiedUserRecord
        {
            // Iteration-1 (bridge proxy) does not have a real version id; the polling
            // A2PartyImportJob will write a real version when it imports this party.
            PartyUuid = profile.UserUuid.Value,
            VersionId = 0UL,
            PartyId = checked((uint)profile.PartyId),
            OwnerUuid = FieldValue.Null,
            ExternalUrn = externalUrn,
            DisplayName = displayName,
            PersonIdentifier = FieldValue.Null,
            OrganizationIdentifier = FieldValue.Null,
            CreatedAt = now,
            ModifiedAt = now,
            User = new PartyUserRecord(userId, profile.UserName),
            IsDeleted = false,
            DeletedAt = FieldValue.Null,
            SelfIdentifiedUserType = request.SelfIdentifiedUserType,
            Email = email,
            ExtRef = extRef,
        };
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Error, "SBL Bridge response missing one or more required identifiers (UserUuid, UserId, PartyId, UserName)")]
        public static partial void IncompleteBridgeResponse(ILogger logger);

        [LoggerMessage(1, LogLevel.Warning, "Failed to enqueue local profile import for newly created self-identified user {PartyUuid}.")]
        public static partial void EnqueueImportFailed(ILogger logger, Exception exception, Guid partyUuid);
    }
}

/// <summary>
/// Direct-DB implementation of get-or-create self-identified user (iteration 2).
/// </summary>
/// <remarks>
/// Stubbed for now; switching to this handler is the iteration-2 cutover. See issue #863.
/// </remarks>
internal sealed class GetOrCreateSelfIdentifiedUserFromDBHandler
    : IRequestHandler<GetOrCreateSelfIdentifiedUserRequest, SelfIdentifiedUserRecord>
{
    /// <inheritdoc/>
    public ValueTask<Result<SelfIdentifiedUserRecord>> Handle(
        GetOrCreateSelfIdentifiedUserRequest request,
        CancellationToken cancellationToken)
        => throw new NotImplementedException("Iteration 2: direct DB write not yet implemented. See issue #863.");
}
