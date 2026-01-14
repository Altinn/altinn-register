using Altinn.Authorization.ModelUtils;

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
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001"
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
}
