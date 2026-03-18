using System.Diagnostics;
using Altinn.Register.Core.Parties.Records;
using V1Models = Altinn.Register.Contracts.V1;

namespace Altinn.Register.Core.Operations;

/// <summary>
/// Maps party records to v1 contract models.
/// </summary>
internal static class V1PartyMapper
{
    /// <summary>
    /// Maps a <see cref="PartyRecord"/> to a <see cref="V1Models.Party"/>.
    /// </summary>
    /// <param name="party">The party record.</param>
    /// <returns>The mapped v1 party.</returns>
    internal static V1Models.Party ToV1Party(PartyRecord party)
    {
        var ret = new V1Models.Party
        {
            PartyUuid = party.PartyUuid.Value,
            PartyId = checked((int)party.PartyId.Value),
            Name = party.DisplayName.Value,
            IsDeleted = party.IsDeleted.Value,
            OnlyHierarchyElementWithNoAccess = false,
        };

        switch (party)
        {
            case OrganizationRecord org:
                ret.PartyTypeName = V1Models.PartyType.Organisation;
                ret.OrgNumber = org.OrganizationIdentifier.Value!.ToString();
                ret.Organization = ToV1Organization(org);
                break;

            case PersonRecord person:
                ret.PartyTypeName = V1Models.PartyType.Person;
                ret.SSN = person.PersonIdentifier.Value!.ToString();
                ret.Name = person.ShortName.HasValue ? person.ShortName.Value : person.DisplayName.Value;
                ret.Person = ToV1Person(person);
                break;

            default:
                throw new UnreachableException($"Unsupported party type: {party.GetType().Name}");
        }

        return ret;
    }

    /// <summary>
    /// Maps an <see cref="OrganizationRecord"/> to a <see cref="V1Models.Organization"/>.
    /// </summary>
    /// <param name="org">The organization record.</param>
    /// <returns>The mapped v1 organization.</returns>
    internal static V1Models.Organization ToV1Organization(OrganizationRecord org)
    {
        var ret = new V1Models.Organization
        {
            OrgNumber = org.OrganizationIdentifier.Value!.ToString(),
            Name = org.DisplayName.Value,
            UnitType = org.UnitType.Value,
            TelephoneNumber = org.TelephoneNumber.Value,
            MobileNumber = org.MobileNumber.Value,
            FaxNumber = org.FaxNumber.Value,
            EMailAddress = org.EmailAddress.Value,
            InternetAddress = org.InternetAddress.Value,
            UnitStatus = org.UnitStatus.Value,
        };

        if (org.MailingAddress.HasValue)
        {
            var mailingAddress = org.MailingAddress.Value;
            ret.MailingAddress = mailingAddress.Address;
            ret.MailingPostalCode = mailingAddress.PostalCode;
            ret.MailingPostalCity = mailingAddress.City;
        }

        if (org.BusinessAddress.HasValue)
        {
            var businessAddress = org.BusinessAddress.Value;
            ret.BusinessAddress = businessAddress.Address;
            ret.BusinessPostalCode = businessAddress.PostalCode;
            ret.BusinessPostalCity = businessAddress.City;
        }

        return ret;
    }

    /// <summary>
    /// Maps a <see cref="PersonRecord"/> to a <see cref="V1Models.Person"/>.
    /// </summary>
    /// <param name="person">The person record.</param>
    /// <returns>The mapped v1 person.</returns>
    private static V1Models.Person ToV1Person(PersonRecord person)
    {
        var ret = new V1Models.Person
        {
            SSN = person.PersonIdentifier.Value!.ToString(),
            Name = person.ShortName.HasValue ? person.ShortName.Value : person.DisplayName.Value,
            FirstName = person.FirstName.Value,
            MiddleName = person.MiddleName.Value,
            LastName = person.LastName.Value,
        };

        if (person.MailingAddress.HasValue)
        {
            ret.MailingAddress = person.MailingAddress.Value.Address;
            ret.MailingPostalCode = person.MailingAddress.Value.PostalCode;
            ret.MailingPostalCity = person.MailingAddress.Value.City;
        }

        if (person.Address.HasValue)
        {
            ret.AddressMunicipalNumber = person.Address.Value.MunicipalNumber;
            ret.AddressMunicipalName = person.Address.Value.MunicipalName;
            ret.AddressStreetName = person.Address.Value.StreetName;
            ret.AddressHouseNumber = person.Address.Value.HouseNumber;
            ret.AddressHouseLetter = person.Address.Value.HouseLetter;
            ret.AddressPostalCode = person.Address.Value.PostalCode;
            ret.AddressCity = person.Address.Value.City;
        }

        if (person.DateOfDeath.HasValue)
        {
            ret.DateOfDeath = person.DateOfDeath.Value.ToDateTime(TimeOnly.MinValue);
        }

        return ret;
    }
}
