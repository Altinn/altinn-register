using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Contracts;
using Altinn.Register.Contracts.PartyImport.A2;
using Altinn.Register.Core.A2.SblProfile;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Mediator;
using Altinn.Urn;
using Microsoft.Extensions.Logging;

namespace Altinn.Register.Core.Operations;

#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Request to get an existing self-identified user, or create one if none exists.
/// </summary>
/// <param name="SelfIdentifiedUserType">
/// The self-identified user type. Only <see cref="SelfIdentifiedUserType.Legacy"/>
/// and <see cref="SelfIdentifiedUserType.IdPortenEmail"/> are supported.
/// </param>
/// <param name="Email">
/// The user email (required for <see cref="SelfIdentifiedUserType.IdPortenEmail"/>).
/// </param>
/// <param name="Issuer">
/// The OIDC issuer (required for <see cref="SelfIdentifiedUserType.Legacy"/>).
/// </param>
/// <param name="ExternalSubject">
/// The OIDC subject / external identity (required for <see cref="SelfIdentifiedUserType.Legacy"/>).
/// </param>
/// <param name="UserNamePrefix">
/// Optional username prefix. The handler generates a unique suffix.
/// </param>
public readonly record struct GetOrCreateSelfIdentifiedUserRequest(
    SelfIdentifiedUserType SelfIdentifiedUserType,
    string? Email,
    string? Issuer,
    string? ExternalSubject,
    string? UserNamePrefix)
    : IRequest<SelfIdentifiedUserResult>;

/// <summary>
/// Result of <see cref="GetOrCreateSelfIdentifiedUserRequest"/>.
/// </summary>
/// <param name="PartyUuid">The party UUID assigned to the user.</param>
/// <param name="PartyId">The legacy numeric party id.</param>
/// <param name="UserId">The legacy numeric user id.</param>
/// <param name="UserName">The username (server-generated when created).</param>
/// <param name="SelfIdentifiedUserType">The self-identified user type.</param>
/// <param name="ExternalUrn">
/// The canonical external URN representing the user. <see langword="null"/> for
/// <see cref="SelfIdentifiedUserType.Educational"/> (the DB stores edu users with no <c>ext_urn</c>).
/// </param>
public readonly record struct SelfIdentifiedUserResult(
    Guid PartyUuid,
    uint PartyId,
    uint UserId,
    string UserName,
    SelfIdentifiedUserType SelfIdentifiedUserType,
    string? ExternalUrn);

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
    ILogger<GetOrCreateSelfIdentifiedUserFromBridgeHandler> logger)
    : IRequestHandler<GetOrCreateSelfIdentifiedUserRequest, SelfIdentifiedUserResult>
{
    private const string IdPortenEmailUrnPrefix = "urn:altinn:person:idporten-email";
    private const string LegacySelfIdentifiedUrnPrefix = "urn:altinn:person:legacy-selfidentified";
    private const string DefaultUserNamePrefix = "altinn-";
    private const int SbiUserTypeSelfIdentified = 2;

    private static readonly Regex _userNameRegex = UserNameRegex();

    /// <inheritdoc/>
    public async ValueTask<Result<SelfIdentifiedUserResult>> Handle(
        GetOrCreateSelfIdentifiedUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryBuildBridgeExternalIdentity(request, out var bridgeExternalIdentity, out var validationProblem))
        {
            return validationProblem;
        }

        var lookupResult = await bridgeClient.LookupUser(bridgeExternalIdentity, cancellationToken);
        if (lookupResult.IsProblem)
        {
            return lookupResult.Problem;
        }

        if (lookupResult.Value.Found)
        {
            return MapToResult(lookupResult.Value.Profile, request.SelfIdentifiedUserType);
        }

        var userName = GenerateUserName(bridgeExternalIdentity, request.UserNamePrefix);
        var createRequest = new SblUserProfile
        {
            ExternalIdentity = bridgeExternalIdentity,
            UserName = userName,
            UserType = SbiUserTypeSelfIdentified,
        };

        var createResult = await bridgeClient.CreateUser(createRequest, cancellationToken);
        if (createResult.IsProblem)
        {
            return createResult.Problem;
        }

        var mapped = MapToResult(createResult.Value, request.SelfIdentifiedUserType);
        if (mapped.IsProblem)
        {
            return mapped.Problem;
        }

        await EnqueueLocalImport(mapped.Value, cancellationToken);
        return mapped;
    }

    // The polling A2PartyImportJob will eventually pick up this party as a change
    // from SBL, but we enqueue the same command eagerly so authentication can read
    // the party from local register on the immediate next request. ChangeId is 0 and
    // Tracking is left default — the polling job will track ProcessedMax when it
    // re-processes the same change.
    private async ValueTask EnqueueLocalImport(SelfIdentifiedUserResult user, CancellationToken cancellationToken)
    {
        try
        {
            await commandSender.Send(
                new ImportA2PartyCommand
                {
                    PartyUuid = user.PartyUuid,
                    ChangeId = 0,
                    ChangedTime = DateTimeOffset.UtcNow,
                    Tracking = default,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Don't fail the create on a bus hiccup — polling job will reconcile.
            Log.EnqueueImportFailed(logger, ex, user.PartyUuid);
        }
    }

    private static bool TryBuildBridgeExternalIdentity(
        GetOrCreateSelfIdentifiedUserRequest request,
        [NotNullWhen(true)] out string? bridgeExternalIdentity,
        [NotNullWhen(false)] out ProblemInstance? validationProblem)
    {
        switch (request.SelfIdentifiedUserType)
        {
            case SelfIdentifiedUserType.IdPortenEmail:
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    bridgeExternalIdentity = null;
                    validationProblem = Problems.SelfIdentifiedUserTypeMismatch.Create([
                        new("reason", "email required for idporten-email"),
                    ]);
                    return false;
                }

                if (!string.IsNullOrEmpty(request.Issuer) || !string.IsNullOrEmpty(request.ExternalSubject))
                {
                    bridgeExternalIdentity = null;
                    validationProblem = Problems.SelfIdentifiedUserTypeMismatch.Create([
                        new("reason", "issuer/externalSubject must be empty for idporten-email"),
                    ]);
                    return false;
                }

                var encodedEmail = UrnEncoded.Create(request.Email.ToLowerInvariant());
                bridgeExternalIdentity = $"{IdPortenEmailUrnPrefix}:{encodedEmail.Encoded}";
                validationProblem = null;
                return true;

            case SelfIdentifiedUserType.Legacy:
            case SelfIdentifiedUserType.Educational:
                if (string.IsNullOrWhiteSpace(request.Issuer) || string.IsNullOrWhiteSpace(request.ExternalSubject))
                {
                    bridgeExternalIdentity = null;
                    validationProblem = Problems.SelfIdentifiedUserTypeMismatch.Create([
                        new("reason", "issuer and externalSubject required for legacy/edu"),
                    ]);
                    return false;
                }

                if (!string.IsNullOrEmpty(request.Email))
                {
                    bridgeExternalIdentity = null;
                    validationProblem = Problems.SelfIdentifiedUserTypeMismatch.Create([
                        new("reason", "email must be empty for legacy/edu"),
                    ]);
                    return false;
                }

                bridgeExternalIdentity = $"{request.Issuer}:{request.ExternalSubject}";
                validationProblem = null;
                return true;

            default:
                bridgeExternalIdentity = null;
                validationProblem = Problems.SelfIdentifiedUserTypeMismatch.Create([
                    new("selfIdentifiedUserType", request.SelfIdentifiedUserType.ToString()),
                    new("reason", "unsupported type"),
                ]);
                return false;
        }
    }

    private Result<SelfIdentifiedUserResult> MapToResult(SblUserProfile profile, SelfIdentifiedUserType type)
    {
        if (profile.UserUuid is null || profile.UserId <= 0 || profile.PartyId <= 0 || string.IsNullOrEmpty(profile.UserName))
        {
            Log.IncompleteBridgeResponse(logger);
            return Problems.SelfIdentifiedUserCreateFailed.Create([
                new("reason", "bridge response missing required identifiers"),
            ]);
        }

        string? externalUrn = type switch
        {
            SelfIdentifiedUserType.IdPortenEmail => profile.ExternalIdentity ?? string.Empty,
            SelfIdentifiedUserType.Legacy => $"{LegacySelfIdentifiedUrnPrefix}:{UrnEncoded.Create(profile.UserName.ToLowerInvariant()).Encoded}",
            SelfIdentifiedUserType.Educational => null,
            _ => string.Empty,
        };

        return new SelfIdentifiedUserResult(
            PartyUuid: profile.UserUuid.Value,
            PartyId: checked((uint)profile.PartyId),
            UserId: checked((uint)profile.UserId),
            UserName: profile.UserName,
            SelfIdentifiedUserType: type,
            ExternalUrn: externalUrn);
    }

    /// <summary>
    /// Generates a username from the bridge-shaped external identity, using a stable hashed
    /// segment plus a random suffix so concurrent creates do not collide.
    /// </summary>
    /// <param name="externalIdentity">The bridge-shaped external identity string.</param>
    /// <param name="userNamePrefix">Optional prefix; defaults to <c>altinn-</c> when null or empty.</param>
    /// <returns>The generated username.</returns>
    internal static string GenerateUserName(string externalIdentity, string? userNamePrefix)
    {
        Span<byte> hashBuffer = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(externalIdentity), hashBuffer);

        var hashSegment = Convert.ToBase64String(hashBuffer);
        var trimmed = _userNameRegex.Replace(hashSegment, string.Empty);
        var truncated = trimmed.Length >= 10 ? trimmed[..10] : trimmed;
        var lowered = truncated.ToLowerInvariant();

        Span<byte> random = stackalloc byte[6];
        RandomNumberGenerator.Fill(random);
        var randomSegment = Convert.ToBase64String(random)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var prefix = string.IsNullOrEmpty(userNamePrefix) ? DefaultUserNamePrefix : userNamePrefix;
        return prefix + lowered + randomSegment;
    }

    [GeneratedRegex("[^a-zA-Z0-9-]")]
    private static partial Regex UserNameRegex();

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
    : IRequestHandler<GetOrCreateSelfIdentifiedUserRequest, SelfIdentifiedUserResult>
{
    /// <inheritdoc/>
    public ValueTask<Result<SelfIdentifiedUserResult>> Handle(
        GetOrCreateSelfIdentifiedUserRequest request,
        CancellationToken cancellationToken)
        => throw new NotImplementedException("Iteration 2: direct DB write not yet implemented. See issue #863.");
}
