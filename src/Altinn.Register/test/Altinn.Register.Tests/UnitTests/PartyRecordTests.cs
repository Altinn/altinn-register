#nullable enable

using System.Text.Json;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Utils;
using Altinn.Register.TestUtils;

namespace Altinn.Register.Tests.UnitTests;

public class PartyRecordTests
    : HostTestBase
{
    private static readonly JsonSerializerOptions _options
        = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public void Serialize_PartyRecord_AllUnset()
    {
        PartyRecord record = new PartyRecord(FieldValue.Unset)
        {
            PartyUuid = FieldValue.Unset,
            PartyId = FieldValue.Unset,
            Name = FieldValue.Unset,
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = FieldValue.Unset,
            IsDeleted = FieldValue.Unset,
            VersionId = FieldValue.Unset,
        };

        var json = JsonSerializer.Serialize(record, _options);
        json.Should().Be("""{}""");

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<PartyRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_PartyRecord_TypeUnset()
    {
        PartyRecord record = new PartyRecord(FieldValue.Unset)
        {
            PartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PartyId = 1,
            Name = "1",
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            VersionId = 50,
        };

        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyUuid":"00000000-0000-0000-0000-000000000001",
                "partyId":1,
                "name":"1",
                "createdAt":"2000-01-01T00:00:00+00:00",
                "modifiedAt":"2000-01-01T00:00:00+00:00",
                "isDeleted":false,
                "versionId":50
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<PartyRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_PartyRecord_PersonType()
    {
        PartyRecord record = new PartyRecord(PartyType.Person)
        {
            PartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PartyId = 1,
            Name = "1",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = true,
            VersionId = 42,
        };

        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "person",
                "partyUuid":"00000000-0000-0000-0000-000000000001",
                "partyId":1,
                "name":"1",
                "personIdentifier":"25871999336",
                "createdAt":"2000-01-01T00:00:00+00:00",
                "modifiedAt":"2000-01-01T00:00:00+00:00",
                "isDeleted":true,
                "versionId":42
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<PersonRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_PartyRecord_OrganizationType()
    {
        PartyRecord record = new PartyRecord(PartyType.Organization)
        {
            PartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PartyId = 1,
            Name = "1",
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = OrganizationIdentifier.Parse("123456785"),
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            VersionId = 42,
        };

        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "organization",
                "partyUuid":"00000000-0000-0000-0000-000000000001",
                "partyId":1,
                "name":"1",
                "organizationIdentifier":"123456785",
                "createdAt":"2000-01-01T00:00:00+00:00",
                "modifiedAt":"2000-01-01T00:00:00+00:00",
                "isDeleted":false,
                "versionId":42
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<OrganizationRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_PersonRecord_Empty()
    {
        PartyRecord record = new PersonRecord
        {
            PartyUuid = FieldValue.Unset,
            PartyId = FieldValue.Unset,
            Name = FieldValue.Unset,
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = FieldValue.Unset,
            IsDeleted = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            FirstName = FieldValue.Unset,
            MiddleName = FieldValue.Unset,
            LastName = FieldValue.Unset,
            Address = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            DateOfBirth = FieldValue.Unset,
            DateOfDeath = FieldValue.Unset,
        };

        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "person"
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<PersonRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_PersonRecord_OnlyPartyFields()
    {
        PartyRecord record = new PersonRecord
        {
            PartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PartyId = 1,
            Name = "1",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            VersionId = 42,

            FirstName = FieldValue.Unset,
            MiddleName = FieldValue.Unset,
            LastName = FieldValue.Unset,
            Address = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            DateOfBirth = FieldValue.Unset,
            DateOfDeath = FieldValue.Unset,
        };

        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "person",
                "partyUuid":"00000000-0000-0000-0000-000000000001",
                "partyId":1,
                "name":"1",
                "personIdentifier":"25871999336",
                "createdAt":"2000-01-01T00:00:00+00:00",
                "modifiedAt":"2000-01-01T00:00:00+00:00",
                "isDeleted":false,
                "versionId":42
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<PersonRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_PersonRecord_Full()
    {
        PartyRecord record = new PersonRecord
        {
            PartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PartyId = 1,
            Name = "1",
            PersonIdentifier = PersonIdentifier.Parse("25871999336"),
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow() + TimeSpan.FromDays(1),
            IsDeleted = true,
            VersionId = 42,

            FirstName = "First",
            MiddleName = null,
            LastName = "Last",
            Address = new StreetAddress
            {
                MunicipalNumber = "1",
                MunicipalName = "2",
                StreetName = "3",
                HouseNumber = "4",
                HouseLetter = "5",
                PostalCode = "6",
                City = "7",
            },
            MailingAddress = new MailingAddress
            {
                Address = "Address",
                PostalCode = "PostalCode",
                City = "City",
            },
            DateOfBirth = new DateOnly(1900, 01, 01),
            DateOfDeath = new DateOnly(2000, 02, 02),
        };

        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "person",
                "partyUuid": "00000000-0000-0000-0000-000000000001",
                "partyId": 1,
                "name": "1",
                "personIdentifier": "25871999336",
                "createdAt": "2000-01-01T00:00:00+00:00",
                "modifiedAt": "2000-01-02T00:00:00+00:00",
                "isDeleted": true,
                "versionId": 42,
                "firstName": "First",
                "middleName": null,
                "lastName": "Last",
                "address": {
                    "municipalNumber": "1",
                    "municipalName": "2",
                    "streetName": "3",
                    "houseNumber": "4",
                    "houseLetter": "5",
                    "postalCode": "6",
                    "city": "7"
                },
                "mailingAddress": {
                    "address": "Address",
                    "postalCode": "PostalCode",
                    "city": "City"
                },
                "dateOfBirth": "1900-01-01",
                "dateOfDeath": "2000-02-02"
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<PersonRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_OrganizationRecord_Empty()
    {
        PartyRecord record = new OrganizationRecord
        {
            PartyUuid = FieldValue.Unset,
            PartyId = FieldValue.Unset,
            Name = FieldValue.Unset,
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = FieldValue.Unset,
            IsDeleted = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            UnitStatus = FieldValue.Unset,
            UnitType = FieldValue.Unset,
            TelephoneNumber = FieldValue.Unset,
            MobileNumber = FieldValue.Unset,
            FaxNumber = FieldValue.Unset,
            EmailAddress = FieldValue.Unset,
            InternetAddress = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            BusinessAddress = FieldValue.Unset,
        };
        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "organization"
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<OrganizationRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_OrganizationRecord_Full()
    {
        PartyRecord record = new OrganizationRecord
        {
            PartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PartyId = 1,
            Name = "1",
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = OrganizationIdentifier.Parse("123456785"),
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            VersionId = 42,

            UnitStatus = "status",
            UnitType = "type",
            TelephoneNumber = "telephone",
            MobileNumber = "mobile",
            FaxNumber = "fax",
            EmailAddress = "email",
            InternetAddress = "internet",
            MailingAddress = new MailingAddress
            {
                Address = "mailing address",
                PostalCode = "mailing postal",
                City = "mailing city",
            },
            BusinessAddress = new MailingAddress
            {
                Address = "business address",
                PostalCode = "business postal",
                City = "business city",
            },
        };

        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "organization",
                "partyUuid":"00000000-0000-0000-0000-000000000001",
                "partyId":1,
                "name":"1",
                "organizationIdentifier":"123456785",
                "createdAt":"2000-01-01T00:00:00+00:00",
                "modifiedAt":"2000-01-01T00:00:00+00:00",
                "isDeleted":false,
                "versionId":42,
                "unitStatus":"status",
                "unitType":"type",
                "telephoneNumber":"telephone",
                "mobileNumber":"mobile",
                "faxNumber":"fax",
                "emailAddress":"email",
                "internetAddress":"internet",
                "mailingAddress": {
                    "address": "mailing address",
                    "postalCode": "mailing postal",
                    "city": "mailing city"
                },
                "businessAddress": {
                    "address": "business address",
                    "postalCode": "business postal",
                    "city": "business city"
                }
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, _options);
        deserialized.Should().BeOfType<OrganizationRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_OrganizationRecord_ParentOrganizationUuid_IsIgnored()
    {
        PartyRecord record = new OrganizationRecord
        {
            PartyUuid = FieldValue.Unset,
            PartyId = FieldValue.Unset,
            Name = FieldValue.Unset,
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = FieldValue.Unset,
            CreatedAt = FieldValue.Unset,
            ModifiedAt = FieldValue.Unset,
            IsDeleted = FieldValue.Unset,
            VersionId = FieldValue.Unset,

            UnitStatus = FieldValue.Unset,
            UnitType = FieldValue.Unset,
            TelephoneNumber = FieldValue.Unset,
            MobileNumber = FieldValue.Unset,
            FaxNumber = FieldValue.Unset,
            EmailAddress = FieldValue.Unset,
            InternetAddress = FieldValue.Unset,
            MailingAddress = FieldValue.Unset,
            BusinessAddress = FieldValue.Unset,

            ParentOrganizationUuid = Guid.Parse("00000000-0000-0000-0000-000000000002"),
        };
        var json = JsonSerializer.SerializeToElement(record, _options);
        json.Should().BeEquivalentTo(
            """
            {
                "partyType": "organization"
            }
            """);

        var json2 =
            """
            {
                "partyType": "organization",
                "parentOrganizationUuid": "00000000-0000-0000-0000-000000000002"
            }
            """;

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json2, _options);
        var org = deserialized.Should().BeOfType<OrganizationRecord>().Which;
        org.ParentOrganizationUuid.Should().BeUnset();
    }

    [Fact]
    public void Serialize_RespectsOptions()
    {
        var options = JsonSerializerOptions.Default;

        PartyRecord record = new OrganizationRecord
        {
            PartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PartyId = 1,
            Name = "1",
            PersonIdentifier = FieldValue.Unset,
            OrganizationIdentifier = OrganizationIdentifier.Parse("123456785"),
            CreatedAt = TimeProvider.GetUtcNow(),
            ModifiedAt = TimeProvider.GetUtcNow(),
            IsDeleted = false,
            VersionId = 42,

            UnitStatus = "status",
            UnitType = "type",
            TelephoneNumber = "telephone",
            MobileNumber = "mobile",
            FaxNumber = "fax",
            EmailAddress = "email",
            InternetAddress = "internet",
            MailingAddress = new MailingAddress
            {
                Address = "mailing address",
                PostalCode = "mailing postal",
                City = "mailing city",
            },
            BusinessAddress = new MailingAddress
            {
                Address = "business address",
                PostalCode = "business postal",
                City = "business city",
            },
        };

        var json = JsonSerializer.SerializeToElement(record, options);
        json.Should().BeEquivalentTo(
            """
            {
                "PartyType": "organization",
                "PartyUuid":"00000000-0000-0000-0000-000000000001",
                "PartyId":1,
                "Name":"1",
                "OrganizationIdentifier":"123456785",
                "CreatedAt":"2000-01-01T00:00:00+00:00",
                "ModifiedAt":"2000-01-01T00:00:00+00:00",
                "IsDeleted":false,
                "VersionId":42,
                "UnitStatus":"status",
                "UnitType":"type",
                "TelephoneNumber":"telephone",
                "MobileNumber":"mobile",
                "FaxNumber":"fax",
                "EmailAddress":"email",
                "InternetAddress":"internet",
                "MailingAddress": {
                    "Address": "mailing address",
                    "PostalCode": "mailing postal",
                    "City": "mailing city"
                },
                "BusinessAddress": {
                    "Address": "business address",
                    "PostalCode": "business postal",
                    "City": "business city"
                }
            }
            """);

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, options);
        deserialized.Should().BeOfType<OrganizationRecord>();
        deserialized.Should().BeEquivalentTo(record);
    }

    [Fact]
    public void Serialize_SupportsCaseInsensitive()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNameCaseInsensitive = true,
        };

        var json =
            """
            {
                "pARTYtYPE": "organization",
                "NAME": "Test",
                "UNITTYPE": "type"
            }
            """;

        var deserialized = JsonSerializer.Deserialize<PartyRecord>(json, options);
        var org = deserialized.Should().BeOfType<OrganizationRecord>().Which;
        org.Name.Should().HaveValue().Which.Should().Be("Test");
        org.UnitType.Should().HaveValue().Which.Should().Be("type");
    }

    [Fact]
    public void Deserialize_Subclasses()
    {
        JsonSerializer.Deserialize<PersonRecord>("""{}""", _options).Should().NotBeNull();
        JsonSerializer.Deserialize<OrganizationRecord>("""{}""", _options).Should().NotBeNull();
    }
}
