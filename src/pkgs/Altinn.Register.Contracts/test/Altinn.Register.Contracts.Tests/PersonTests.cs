using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Contracts.Tests;

public class PersonTests
    : PartyTests
{
    protected static PersonIdentifier PersonIdentifier { get; }
        = PersonIdentifier.Parse("25871999336");

    [Fact]
    public async Task MinimalPerson()
    {
        await ValidateParty(
            new Person
            {
                Uuid = Uuid,
                PartyId = FieldValue.Unset,
                DisplayName = FieldValue.Unset,
                CreatedAt = FieldValue.Unset,
                ModifiedAt = FieldValue.Unset,
                IsDeleted = FieldValue.Unset,
                User = FieldValue.Unset,
                VersionId = VersionId,

                PersonIdentifier = PersonIdentifier,
                FirstName = FieldValue.Unset,
                MiddleName = FieldValue.Unset,
                LastName = FieldValue.Unset,
                ShortName = FieldValue.Unset,
                Address = FieldValue.Unset,
                MailingAddress = FieldValue.Unset,
                DateOfBirth = FieldValue.Unset,
                DateOfDeath = FieldValue.Unset,
            },
            """
            {
              "partyType": "person",
              "partyUuid": "00000000-0000-0000-0000-000000000001",
              "personIdentifier": "25871999336",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001"
            }
            """);
    }

    [Fact]
    public async Task MaximalPerson()
    {
        await ValidateParty(
            new Person
            {
                Uuid = Uuid,
                PartyId = PartyId,
                DisplayName = "Display Name",
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                IsDeleted = false,
                User = FullUser,
                VersionId = VersionId,

                PersonIdentifier = PersonIdentifier,
                FirstName = "First",
                MiddleName = "Middle",
                LastName = "Last",
                ShortName = "First Middle Last",
                Address = new StreetAddress
                {
                    MunicipalNumber = "123",
                    MunicipalName = "Municipal Name",
                    StreetName = "Street Name",
                    HouseNumber = "42",
                    HouseLetter = "A",
                    PostalCode = "1234",
                    City = "City Name",
                },
                MailingAddress = new MailingAddress
                {
                    Address = "Address Lines",
                    PostalCode = "1234",
                    City = "City Name",
                },
                DateOfBirth = new DateOnly(2020, 01, 02),
                DateOfDeath = FieldValue.Null,
            },
            """
            {
              "partyType": "person",
              "partyUuid": "00000000-0000-0000-0000-000000000001",
              "personIdentifier": "25871999336",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001",
              "partyId": 12345678,
              "displayName": "Display Name",
              "createdAt": "2020-01-02T03:04:05+00:00",
              "modifiedAt": "2022-05-06T07:08:09+00:00",
              "isDeleted": false,
              "user": {
                "userId": 50,
                "username": "username",
                "userIds": [ 50, 30, 1 ]
              },
              "firstName": "First",
              "middleName": "Middle",
              "lastName": "Last",
              "shortName": "First Middle Last",
              "address": {
                "municipalNumber": "123",
                "municipalName": "Municipal Name",
                "streetName": "Street Name",
                "houseNumber": "42",
                "houseLetter": "A",
                "postalCode": "1234",
                "city": "City Name"
              },
              "mailingAddress": {
                "address": "Address Lines",
                "postalCode": "1234",
                "city": "City Name"
              },
              "dateOfBirth": "2020-01-02",
              "dateOfDeath": null
            }
            """);
    }

    [Fact]
    public async Task DeadPerson()
    {
        await ValidateParty(
            new Person
            {
                Uuid = Uuid,
                PartyId = PartyId,
                DisplayName = "Display Name",
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                IsDeleted = true,
                User = FullUser,
                VersionId = VersionId,

                PersonIdentifier = PersonIdentifier,
                FirstName = "First",
                MiddleName = "Middle",
                LastName = "Last",
                ShortName = "First Middle Last",
                Address = new StreetAddress
                {
                    MunicipalNumber = "123",
                    MunicipalName = "Municipal Name",
                    StreetName = "Street Name",
                    HouseNumber = "42",
                    HouseLetter = "A",
                    PostalCode = "1234",
                    City = "City Name",
                },
                MailingAddress = new MailingAddress
                {
                    Address = "Address Lines",
                    PostalCode = "1234",
                    City = "City Name",
                },
                DateOfBirth = new DateOnly(1920, 01, 02),
                DateOfDeath = new DateOnly(2000, 04, 05),
            },
            """
            {
              "partyType": "person",
              "partyUuid": "00000000-0000-0000-0000-000000000001",
              "personIdentifier": "25871999336",
              "versionId": 1,
              "urn": "urn:altinn:party:uuid:00000000-0000-0000-0000-000000000001",
              "partyId": 12345678,
              "displayName": "Display Name",
              "createdAt": "2020-01-02T03:04:05+00:00",
              "modifiedAt": "2022-05-06T07:08:09+00:00",
              "isDeleted": true,
              "user": {
                "userId": 50,
                "username": "username",
                "userIds": [ 50, 30, 1 ]
              },
              "firstName": "First",
              "middleName": "Middle",
              "lastName": "Last",
              "shortName": "First Middle Last",
              "address": {
                "municipalNumber": "123",
                "municipalName": "Municipal Name",
                "streetName": "Street Name",
                "houseNumber": "42",
                "houseLetter": "A",
                "postalCode": "1234",
                "city": "City Name"
              },
              "mailingAddress": {
                "address": "Address Lines",
                "postalCode": "1234",
                "city": "City Name"
              },
              "dateOfBirth": "1920-01-02",
              "dateOfDeath": "2000-04-05"
            }
            """);
    }
}
