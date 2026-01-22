using Altinn.Authorization.ModelUtils;
using Altinn.Urn;

namespace Altinn.Register.Contracts.Tests;

public class SelfIdentifiedUserTests
    : PartyTests
{
    [Fact]
    public async Task MinimalSelfIdentifiedUser()
    {
        await ValidateParty(
            new SelfIdentifiedUser
            {
                Uuid = Uuid,
                ExternalUrn = FieldValue.Null,
                PartyId = FieldValue.Unset,
                DisplayName = FieldValue.Unset,
                CreatedAt = FieldValue.Unset,
                ModifiedAt = FieldValue.Unset,
                IsDeleted = FieldValue.Unset,
                DeletedAt = FieldValue.Unset,
                User = FieldValue.Unset,
                VersionId = VersionId,
                SelfIdentifiedUserType = FieldValue.Unset,
            },
            """
            {
              "partyType": "self-identified-user",
              "partyUuid": "00000000-0000-0000-0000-000000000001",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001",
              "externalUrn": null
            }
            """);
    }

    [Fact]
    public async Task MaximalSelfIdentifiedUser()
    {
        await ValidateParty(
            new SelfIdentifiedUser
            {
                Uuid = Uuid,
                ExternalUrn = NonExhaustive.Create<PartyExternalRefUrn>(PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create("username"))),
                PartyId = PartyId,
                DisplayName = "Display Name",
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                IsDeleted = false,
                DeletedAt = FieldValue.Null,
                User = FullUser,
                VersionId = VersionId,
                SelfIdentifiedUserType = NonExhaustiveEnum.Create(SelfIdentifiedUserType.Legacy),
            },
            """
            {
              "partyType": "self-identified-user",
              "partyUuid": "00000000-0000-0000-0000-000000000001",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001",
              "externalUrn": "urn:altinn:person:legacy-selfidentified:username",
              "partyId": 12345678,
              "displayName": "Display Name",
              "createdAt": "2020-01-02T03:04:05+00:00",
              "modifiedAt": "2022-05-06T07:08:09+00:00",
              "isDeleted": false,
              "deletedAt": null,
              "user": {
                "userId": 50,
                "username": "username",
                "userIds": [ 50, 30, 1 ]
              },
              "selfIdentifiedUserType": "legacy"
            }
            """);
    }

    [Fact]
    public async Task MaximalSelfIdentifiedUser_Edu()
    {
        await ValidateParty(
            new SelfIdentifiedUser
            {
                Uuid = Uuid,
                ExternalUrn = FieldValue.Null,
                PartyId = PartyId,
                DisplayName = "Display Name",
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                IsDeleted = false,
                DeletedAt = FieldValue.Null,
                User = FullUser,
                VersionId = VersionId,
                SelfIdentifiedUserType = NonExhaustiveEnum.Create(SelfIdentifiedUserType.Educational),
            },
            """
            {
              "partyType": "self-identified-user",
              "partyUuid": "00000000-0000-0000-0000-000000000001",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001",
              "externalUrn": null,
              "partyId": 12345678,
              "displayName": "Display Name",
              "createdAt": "2020-01-02T03:04:05+00:00",
              "modifiedAt": "2022-05-06T07:08:09+00:00",
              "isDeleted": false,
              "deletedAt": null,
              "user": {
                "userId": 50,
                "username": "username",
                "userIds": [ 50, 30, 1 ]
              },
              "selfIdentifiedUserType": "edu"
            }
            """);
    }

    [Fact]
    public async Task MaximalSelfIdentifiedUser_IDPortenEmail()
    {
        await ValidateParty(
            new SelfIdentifiedUser
            {
                Uuid = Uuid,
                ExternalUrn = NonExhaustive.Create<PartyExternalRefUrn>(PartyExternalRefUrn.IDPortenEmail.Create(UrnEncoded.Create("test@example.com"))),
                PartyId = PartyId,
                DisplayName = "Display Name",
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                IsDeleted = false,
                DeletedAt = FieldValue.Null,
                User = FullUser,
                VersionId = VersionId,
                SelfIdentifiedUserType = NonExhaustiveEnum.Create(SelfIdentifiedUserType.IdPortenEmail),
            },
            """
            {
              "partyType": "self-identified-user",
              "partyUuid": "00000000-0000-0000-0000-000000000001",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001",
              "externalUrn": "urn:altinn:person:idporten-email:test@example.com",
              "partyId": 12345678,
              "displayName": "Display Name",
              "createdAt": "2020-01-02T03:04:05+00:00",
              "modifiedAt": "2022-05-06T07:08:09+00:00",
              "isDeleted": false,
              "deletedAt": null,
              "user": {
                "userId": 50,
                "username": "username",
                "userIds": [ 50, 30, 1 ]
              },
              "selfIdentifiedUserType": "idporten-email"
            }
            """);
    }
}
