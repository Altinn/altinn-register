#nullable enable

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Core.Utils;
using CommunityToolkit.Diagnostics;
using V1Models = Altinn.Platform.Register.Models;
using V1PartyType = Altinn.Platform.Register.Enums.PartyType;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Implementation of <see cref="IA2PartyImportService"/>.
/// </summary>
internal sealed class A2PartyImportService
    : IA2PartyImportService
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportService"/> class.
    /// </summary>
    public A2PartyImportService(HttpClient httpClient, TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<A2PartyChangePage> GetChanges(
        uint fromExclusive = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        PartyChangesResponse response;
        do
        {
            response = await GetChangesPage(fromExclusive, cancellationToken);
            yield return MapChangePage(response);

            fromExclusive = response.LastChangeInSegment;
        }
        while (response.LastAvailableChange != response.LastChangeInSegment);
    }

    /// <inheritdoc />
    public async Task<PartyRecord> GetParty(Guid partyUuid, CancellationToken cancellationToken = default)
    {
        var url = $"parties?partyuuid={partyUuid}";

        var response = await _httpClient.GetFromJsonAsync<V1Models.Party>(url, _options, cancellationToken);

        if (response is null)
        {
            throw new PartyNotFoundException(partyUuid);
        }

        Debug.Assert(response.PartyUuid == partyUuid, "Party UUID mismatch");
        return MapParty(response);
    }

    private async Task<PartyChangesResponse> GetChangesPage(uint fromExclusive, CancellationToken cancellationToken)
    {
        var url = $"parties/partychanges/{fromExclusive}";

        var response = await _httpClient.GetFromJsonAsync<PartyChangesResponse>(url, _options, cancellationToken);
        if (response is null)
        {
            throw new InvalidOperationException("Failed to parse party changes.");
        }

        return response;
    }

    private A2PartyChangePage MapChangePage(PartyChangesResponse response)
    {
        var changes = response.PartyChangeList.Select(static change => new A2PartyChange
        {
            ChangeId = change.ChangeId,
            PartyId = change.PartyId,
            PartyUuid = change.PartyUuid,
            ChangeTime = change.LastChangedTime,
        }).ToImmutableArray();

        return new A2PartyChangePage(changes, response.LastAvailableChange);
    }

    private PartyRecord MapParty(V1Models.Party party)
    {
        return party.PartyTypeName switch
        {
            V1PartyType.Person => MapPerson(party, _timeProvider.GetUtcNow()),
            V1PartyType.Organisation => MapOrganization(party, _timeProvider.GetUtcNow()),
            _ => ThrowHelper.ThrowNotSupportedException<PartyRecord>($"Party type {party.PartyTypeName} is not supported."),
        };

        static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value;

        static StreetAddress? MapStreetAddress(
            string? municipalNumber,
            string? municipalName,
            string? streetName,
            string? houseNumber,
            string? houseLetter,
            string? postalCode,
            string? city)
        {
            municipalNumber = Normalize(municipalNumber);
            municipalName = Normalize(municipalName);
            streetName = Normalize(streetName);
            houseNumber = Normalize(houseNumber);
            houseLetter = Normalize(houseLetter);
            postalCode = Normalize(postalCode);
            city = Normalize(city);

            if (municipalNumber is null
                && municipalName is null
                && streetName is null
                && houseNumber is null
                && houseLetter is null
                && postalCode is null
                && city is null)
            {
                return null;
            }

            return new StreetAddress
            {
                MunicipalNumber = municipalNumber,
                MunicipalName = municipalName,
                StreetName = streetName,
                HouseNumber = houseNumber,
                HouseLetter = houseLetter,
                PostalCode = postalCode,
                City = city,
            };
        }

        static MailingAddress? MapMailingAddress(
            string? address,
            string? postalCode,
            string? city)
        {
            address = Normalize(address);
            postalCode = Normalize(postalCode);
            city = Normalize(city);

            if (address is null
                && postalCode is null
                && city is null)
            {
                return null;
            }

            return new MailingAddress
            {
                Address = address,
                PostalCode = postalCode,
                City = city,
            };
        }

        static DateOnly CalculateDateOfBirth(PersonIdentifier personIdentifier)
        {
            var s = personIdentifier.ToString().AsSpan();
            var d1 = s[0] - '0';
            var d2 = s[1] - '0';
            var m1 = s[2] - '0';
            var m2 = s[3] - '0';
            var y1 = s[4] - '0';
            var y2 = s[5] - '0';
            var i1 = s[6] - '0';
            var i2 = s[7] - '0';
            var i3 = s[8] - '0';

            var dayComponent = (d1 * 10) + d2;
            var monthComponent = (m1 * 10) + m2;
            var yearComponent = (y1 * 10) + y2;
            var individualComponent = (i1 * 100) + (i2 * 10) + i3;

            if (monthComponent >= 80)
            {
                // Test person
                monthComponent -= 80;
            }

            if (dayComponent >= 40)
            {
                // D-number
                dayComponent -= 40;
            }

            var year = individualComponent switch
            {
                >= 500 and < 750 when yearComponent > 54 => 1800 + yearComponent,
                >= 900 when yearComponent > 39 => 1900 + yearComponent,
                >= 500 when yearComponent < 40 => 2000 + yearComponent,
                _ => 1900 + yearComponent,
            };

            return new DateOnly(year, monthComponent, dayComponent);
        }

        static PersonRecord MapPerson(V1Models.Party party, DateTimeOffset now)
        {
            var person = party.Person!;

            var partyUuid = party.PartyUuid!.Value;
            var partyId = party.PartyId;
            var personIdentifier = MapPersonIdentifier(person.SSN.AsSpan().Trim());
            var name = Normalize(person.Name);
            var firstName = Normalize(person.FirstName);
            var middleName = Normalize(person.MiddleName);
            var lastName = Normalize(person.LastName);
            var isDeleted = party.IsDeleted;

            var address = MapStreetAddress(
                municipalNumber: person.AddressMunicipalNumber,
                municipalName: person.AddressMunicipalName,
                streetName: person.AddressStreetName,
                houseNumber: person.AddressHouseNumber,
                houseLetter: person.AddressHouseLetter,
                postalCode: person.AddressPostalCode,
                city: person.AddressCity);

            var mailingAddress = MapMailingAddress(
                address: person.MailingAddress,
                postalCode: person.MailingPostalCode,
                city: person.MailingPostalCity);

            DateOnly dateOfBirth = CalculateDateOfBirth(personIdentifier);
            DateOnly? dateOfDeath = person.DateOfDeath is null ? null : DateOnly.FromDateTime(person.DateOfDeath.Value);

            if (string.IsNullOrEmpty(name))
            {
                var components = new List<string>(3);
                if (!string.IsNullOrEmpty(person.FirstName))
                {
                    components.Add(person.FirstName);
                }

                if (!string.IsNullOrEmpty(person.MiddleName))
                {
                    components.Add(person.MiddleName);
                }

                if (!string.IsNullOrEmpty(person.LastName))
                {
                    components.Add(person.LastName);
                }

                name = string.Join(' ', components);
            }

            return new PersonRecord
            {
                // party fields
                PartyUuid = partyUuid,
                PartyId = partyId,
                Name = name,
                PersonIdentifier = personIdentifier,
                OrganizationIdentifier = null,
                CreatedAt = now,
                ModifiedAt = now,
                IsDeleted = isDeleted,
                VersionId = FieldValue.Unset,

                // person fields
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName,
                Address = address,
                MailingAddress = mailingAddress,
                DateOfBirth = dateOfBirth,
                DateOfDeath = FieldValue.From(dateOfDeath),
            };
        }

        static PersonIdentifier MapPersonIdentifier(ReadOnlySpan<char> source)
        {
            if (source.SequenceEqual("00000000000"))
            {
                ThrowHelper.ThrowArgumentException(nameof(source), "Person identifier cannot be all zeroes.");
            }

            return PersonIdentifier.Parse(source);
        }

        static OrganizationRecord MapOrganization(V1Models.Party party, DateTimeOffset now)
        {
            var organization = party.Organization!;

            var partyUuid = party.PartyUuid!.Value;
            var partyId = party.PartyId;
            var organizationNumber = MapOrganizationIdentifier(organization.OrgNumber.AsSpan().Trim());
            var name = Normalize(organization.Name);
            var unitStatus = Normalize(organization.UnitStatus);
            var unitType = Normalize(organization.UnitType);
            var telephoneNumber = Normalize(organization.TelephoneNumber);
            var mobileNumber = Normalize(organization.MobileNumber);
            var faxNumber = Normalize(organization.FaxNumber);
            var emailAddress = Normalize(organization.EMailAddress);
            var internetAddress = Normalize(organization.InternetAddress);
            var isDeleted = party.IsDeleted;

            var mailingAddress = MapMailingAddress(
                address: organization.MailingAddress,
                postalCode: organization.MailingPostalCode,
                city: organization.MailingPostalCity);

            var businessAddress = MapMailingAddress(
                address: organization.BusinessAddress,
                postalCode: organization.BusinessPostalCode,
                city: organization.BusinessPostalCity);

            return new OrganizationRecord
            {
                // party fields
                PartyUuid = partyUuid,
                PartyId = partyId,
                Name = name,
                PersonIdentifier = null,
                OrganizationIdentifier = organizationNumber,
                CreatedAt = now,
                ModifiedAt = now,
                IsDeleted = isDeleted,
                VersionId = FieldValue.Unset,

                // organization fields
                UnitStatus = unitStatus,
                UnitType = unitType,
                TelephoneNumber = telephoneNumber,
                MobileNumber = mobileNumber,
                FaxNumber = faxNumber,
                EmailAddress = emailAddress,
                InternetAddress = internetAddress,
                MailingAddress = mailingAddress,
                BusinessAddress = businessAddress,
            };
        }

        static OrganizationIdentifier MapOrganizationIdentifier(ReadOnlySpan<char> source)
        {
            // there are some organization numbers that are 8 in length,
            // they should be 0-prefixed.
            if (source.Length == 8)
            {
                Span<char> padded = stackalloc char[9];
                padded[0] = '0';
                source.CopyTo(padded[1..]);
                return OrganizationIdentifier.Parse(padded);
            }

            return OrganizationIdentifier.Parse(source);
        }
    }

    /// <summary>
    /// A2 party change model.
    /// </summary>
    internal sealed class PartyChange
    {
        /// <summary>
        /// Gets the id of this change.
        /// </summary>
        [JsonPropertyName("ChangeId")]
        public required uint ChangeId { get; init; }

        /// <summary>
        /// Gets the party id of the party that changed.
        /// </summary>
        [JsonPropertyName("PartyId")]
        public required int PartyId { get; init; }

        /// <summary>
        /// Gets the party uuid of the party that changed.
        /// </summary>
        [JsonPropertyName("PartyUuid")]
        public required Guid PartyUuid { get; init; }

        /// <summary>
        /// Gets the time of the change.
        /// </summary>
        [JsonPropertyName("LastChangedTime")]
        public required DateTimeOffset LastChangedTime { get; init; }
    }

    /// <summary>
    /// A2 party change page model.
    /// </summary>
    internal sealed class PartyChangesResponse
    {
        /// <summary>
        /// Gets the list of party changes.
        /// </summary>
        [JsonPropertyName("PartyChangeList")]
        public required IReadOnlyList<PartyChange> PartyChangeList { get; init; }

        /// <summary>
        /// Gets the highest change id available at the time of the request.
        /// </summary>
        [JsonPropertyName("LastAvailableChange")]
        public required uint LastAvailableChange { get; init; }

        /// <summary>
        /// Gets the last change id in the page.
        /// </summary>
        [JsonPropertyName("LastChangeInSegment")]
        public required uint LastChangeInSegment { get; init; }
    }

    /// <summary>
    /// Exception thrown when a party is not found.
    /// </summary>
    internal sealed class PartyNotFoundException(Guid partyUuid)
        : InvalidOperationException($"Party {partyUuid} not found")
    {
        /// <summary>
        /// Gets the UUID of the party that was not found.
        /// </summary>
        public Guid PartyUuid => partyUuid;
    }
}
