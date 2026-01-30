using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Saga for importing parties from A2.
/// </summary>
public partial class A2PartyImportSaga
{
    /// <inheritdoc/>
    public static ValueTask<A2PartyImportSagaData> CreateInitialState(IServiceProvider services, ImportA2UserProfileCommand command)
        => ValueTask.FromResult(new A2PartyImportSagaData
        {
            PartyUuid = command.OwnerPartyUuid,
            UserId = command.UserId,
            Tracking = command.Tracking,
        });

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
                ExternalUrn = FieldValue.Null,
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

        async Task UpsertUserRecordAndComplete(A2ProfileRecord profile, CancellationToken cancellationToken)
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
    }
}
