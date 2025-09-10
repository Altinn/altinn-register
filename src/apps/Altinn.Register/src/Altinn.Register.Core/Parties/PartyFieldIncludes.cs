﻿using System.Text.Json.Serialization;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Fields to include when fetching a <see cref="PartyRecord"/>.
/// </summary>
[Flags]
[StringEnumConverter]
public enum PartyFieldIncludes
    : ulong
{
    /// <summary>
    /// No extra information (default).
    /// </summary>
    [JsonStringEnumMemberName("none")]
    None = 0UL,

    /// <summary>
    /// The party UUID.
    /// </summary>
    [JsonStringEnumMemberName("uuid")]
    PartyUuid = 1UL << 0,

    /// <summary>
    /// The party ID.
    /// </summary>
    [JsonStringEnumMemberName("id")]
    PartyId = 1UL << 1,

    /// <summary>
    /// The party type.
    /// </summary>
    [JsonStringEnumMemberName("type")]
    PartyType = 1UL << 2,

    /// <summary>
    /// The party display-name.
    /// </summary>
    [JsonStringEnumMemberName("display-name")]
    PartyDisplayName = 1UL << 3,

    /// <summary>
    /// The person identifier of the party, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person-id")]
    PartyPersonIdentifier = 1UL << 4,

    /// <summary>
    /// The organization identifier of the party, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org-id")]
    PartyOrganizationIdentifier = 1UL << 5,

    /// <summary>
    /// The time when the party was created.
    /// </summary>
    [JsonStringEnumMemberName("created")]
    PartyCreatedAt = 1UL << 6,

    /// <summary>
    /// The time when the party was last modified.
    /// </summary>
    [JsonStringEnumMemberName("modified")]
    PartyModifiedAt = 1UL << 7,

    /// <summary>
    /// Whether the party is deleted.
    /// </summary>
    [JsonStringEnumMemberName("deleted")]
    PartyIsDeleted = 1UL << 8,

    /// <summary>
    /// The version ID of the party.
    /// </summary>
    [JsonStringEnumMemberName("version")]
    PartyVersionId = 1UL << 9,

    /// <summary>
    /// The UUID of the owner party, if any.
    /// </summary>
    PartyOwnerUuid = 1UL << 10,

    /// <summary>
    /// All party identifiers.
    /// </summary>
    [JsonStringEnumMemberName("identifiers")]
    Identifiers = PartyUuid | PartyId | PartyPersonIdentifier | PartyOrganizationIdentifier,

    /// <summary>
    /// All party fields.
    /// </summary>
    [JsonStringEnumMemberName("party")]
    Party = Identifiers | PartyType | PartyDisplayName | PartyCreatedAt | PartyModifiedAt | PartyIsDeleted | PartyVersionId | PartyOwnerUuid,

    /// <summary>
    /// The first name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.first-name")]
    PersonFirstName = 1UL << 11,

    /// <summary>
    /// The middle name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.middle-name")]
    PersonMiddleName = 1UL << 12,

    /// <summary>
    /// The last name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.last-name")]
    PersonLastName = 1UL << 13,

    /// <summary>
    /// The short name of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.short-name")]
    PersonShortName = 1UL << 14,

    /// <summary>
    /// All person name fields.
    /// </summary>
    [JsonStringEnumMemberName("person.name")]
    PersonName = PersonFirstName | PersonMiddleName | PersonLastName | PersonShortName,

    /// <summary>
    /// The address of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.address")]
    PersonAddress = 1UL << 15,

    /// <summary>
    /// The mailing address of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.mailing-address")]
    PersonMailingAddress = 1UL << 16,

    /// <summary>
    /// The date of birth of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.date-of-birth")]
    PersonDateOfBirth = 1UL << 17,

    /// <summary>
    /// The date of death of the person, if the party is a person.
    /// </summary>
    [JsonStringEnumMemberName("person.date-of-death")]
    PersonDateOfDeath = 1UL << 18,

    /// <summary>
    /// All person fields.
    /// </summary>
    [JsonStringEnumMemberName("person")]
    Person = PersonFirstName | PersonMiddleName | PersonLastName | PersonShortName | PersonAddress | PersonMailingAddress | PersonDateOfBirth | PersonDateOfDeath,

    /// <summary>
    /// The organization unit status, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.status")]
    OrganizationUnitStatus = 1UL << 19,

    /// <summary>
    /// The organization unit type, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.type")]
    OrganizationUnitType = 1UL << 20,

    /// <summary>
    /// The organization telephone number, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.telephone")]
    OrganizationTelephoneNumber = 1UL << 21,

    /// <summary>
    /// The organization mobile number, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.mobile")]
    OrganizationMobileNumber = 1UL << 22,

    /// <summary>
    /// The organization fax number, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.fax")]
    OrganizationFaxNumber = 1UL << 23,

    /// <summary>
    /// The organization email address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.email")]
    OrganizationEmailAddress = 1UL << 24,

    /// <summary>
    /// The organization internet address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.internet")]
    OrganizationInternetAddress = 1UL << 25,

    /// <summary>
    /// The organization mailing address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.mailing-address")]
    OrganizationMailingAddress = 1UL << 26,

    /// <summary>
    /// The organization business address, if the party is an organization.
    /// </summary>
    [JsonStringEnumMemberName("org.business-address")]
    OrganizationBusinessAddress = 1UL << 27,

    /// <summary>
    /// All organization fields.
    /// </summary>
    [JsonStringEnumMemberName("org")]
    Organization = OrganizationUnitStatus | OrganizationUnitType | OrganizationTelephoneNumber | OrganizationMobileNumber | OrganizationFaxNumber
        | OrganizationEmailAddress | OrganizationInternetAddress | OrganizationMailingAddress | OrganizationBusinessAddress,

    /// <summary>
    /// All system user fields.
    /// </summary>
    /// <remarks>
    /// This needs to be before <see cref="SystemUserType"/>, as long as <see cref="SystemUser"/> only consists of a single flag value.
    /// This is to get the correct to-string representation, used in queries and tests.
    /// </remarks>
    [JsonStringEnumMemberName("sysuser")]
    SystemUser = SystemUserType,

    /// <summary>
    /// The system user type, if the party is a system user.
    /// </summary>
    [JsonStringEnumMemberName("sysuser.type")]
    SystemUserType = 1UL << 28,

    /// <summary>
    /// Include subunits (if party is an organization).
    /// </summary>
    [JsonStringEnumMemberName("org.subunits")]
    SubUnits = 1UL << 29,

    /// <summary>
    /// The user id(s), if the party has an associated user.
    /// </summary>
    [JsonStringEnumMemberName("user.id")]
    UserId = 1UL << 30,

    /// <summary>
    /// The username, if the party has an associated user.
    /// </summary>
    [JsonStringEnumMemberName("user.name")]
    Username = 1UL << 31,

    /// <summary>
    /// All user fields.
    /// </summary>
    [JsonStringEnumMemberName("user")]
    User = UserId | Username,
}
