#nullable enable

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Core.Utils;
using Altinn.Register.Utils;
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
    public IA2PartyChanges GetChanges(uint fromExclusive = 0, CancellationToken cancellationToken = default)
        => new A2PartyChanges(this, fromExclusive, cancellationToken);

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
            var personIdentifier = PersonIdentifier.Parse(person.SSN.AsSpan().Trim());
            var name = Normalize(person.Name);
            var firstName = Normalize(person.FirstName);
            var middleName = Normalize(person.MiddleName);
            var lastName = Normalize(person.LastName);
            
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

        static OrganizationRecord MapOrganization(V1Models.Party party, DateTimeOffset now)
        {
            var organization = party.Organization!;
            
            var partyUuid = party.PartyUuid!.Value;
            var partyId = party.PartyId;
            var organizationNumber = OrganizationIdentifier.Parse(organization.OrgNumber.AsSpan().Trim());
            var name = Normalize(organization.Name);
            var unitStatus = Normalize(organization.UnitStatus);
            var unitType = Normalize(organization.UnitType);
            var telephoneNumber = Normalize(organization.TelephoneNumber);
            var mobileNumber = Normalize(organization.MobileNumber);
            var faxNumber = Normalize(organization.FaxNumber);
            var emailAddress = Normalize(organization.EMailAddress);
            var internetAddress = Normalize(organization.InternetAddress);
            
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
    }

    private sealed class A2PartyChanges
        : IA2PartyChanges
        , IAsyncEnumerator<A2PartyChange>
    {
        private readonly AsyncLock _lock = new();
        private readonly A2PartyImportService _service;
        private uint _fromExclusive;
        private bool _endOfData;
        private CancellationTokenSource? _combinedTokens;
        private CancellationToken _cancellationToken;
        private PartyChangesResponse? _response;
        private IEnumerator<PartyChange>? _enumerator;
        private A2PartyChange? _current;

        public A2PartyChanges(A2PartyImportService service, uint fromExclusive, CancellationToken cancellationToken)
        {
            _service = service;
            _fromExclusive = fromExclusive;
            _cancellationToken = cancellationToken;
        }

        [DebuggerHidden]
        A2PartyChange IAsyncEnumerator<A2PartyChange>.Current
            => _current!;

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            _combinedTokens?.Dispose();

            return ValueTask.CompletedTask;
        }

        IAsyncEnumerator<A2PartyChange> IAsyncEnumerable<A2PartyChange>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            if (_cancellationToken.Equals(default))
            {
                _cancellationToken = cancellationToken;
            }
            else if (cancellationToken.Equals(_cancellationToken) || cancellationToken.Equals(default))
            {
                // same or default token, do nothing
            }
            else
            {
                _combinedTokens = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);
                _cancellationToken = _combinedTokens.Token;
            }

            return this;
        }

        ValueTask<bool> IAsyncEnumerator<A2PartyChange>.MoveNextAsync()
        {
            if (_endOfData)
            {
                return new(false);
            }

            // happy path - we already have local data
            if (_enumerator is { } enumerator && enumerator.MoveNext())
            {
                _current = Map(enumerator.Current);
                return new(true);
            }

            return new(MoveNextAsyncCore());
        }

        ValueTask<uint> IA2PartyChanges.GetLastChangeId(CancellationToken cancellationToken)
        {
            if (_response is { } response)
            {
                return new(response.LastAvailableChange);
            }

            return new(GetLastChangeIdCore(cancellationToken));
        }

        private async Task<bool> MoveNextAsyncCore()
        {
            await EnsurePage(fetchNext: true);
            return await ((IAsyncEnumerator<A2PartyChange>)this).MoveNextAsync();
        }

        private async Task<uint> GetLastChangeIdCore(CancellationToken cancellationToken)
        {
            if (_response is { } response)
            {
                return response.LastAvailableChange;
            }

            await EnsurePage(fetchNext: true).WaitAsync(cancellationToken);
            return await GetLastChangeIdCore(cancellationToken);
        }

        private async Task EnsurePage(bool fetchNext)
        {
            var cancellationToken = _cancellationToken;
            using var ticket = await _lock.Acquire(cancellationToken);

            // if we don't need to fetch the next page, and a response is already present,
            // somebody else has already done the work for us
            if (!fetchNext && _response is not null)
            {
                return;
            }

            _enumerator?.Dispose();
            _enumerator = null;
            _response = null;

            var fromExclusive = _fromExclusive;
            var response = await _service.GetChangesPage(fromExclusive, cancellationToken);

            _fromExclusive = response.LastChangeInSegment;
            _response = response;
            _endOfData = response.PartyChangeList.Count == 0;
            _enumerator = response.PartyChangeList.GetEnumerator();
        }

        private static A2PartyChange Map(PartyChange change) 
            => new()
            {
                ChangeId = change.ChangeId,
                PartyId = change.PartyId,
                PartyUuid = change.PartyUuid,
                ChangeTime = change.LastChangedTime
            };
    }

    private sealed class PartyChange
    {
        [JsonPropertyName("ChangeId")]
        public required uint ChangeId { get; init; }

        [JsonPropertyName("PartyId")]
        public required int PartyId { get; init; }

        [JsonPropertyName("PartyUuid")]
        public required Guid PartyUuid { get; init; }

        [JsonPropertyName("LastChangedTime")]
        public required DateTimeOffset LastChangedTime { get; init; }
    }

    private sealed class PartyChangesResponse
    {
        [JsonPropertyName("PartyChangeList")]
        public required IReadOnlyList<PartyChange> PartyChangeList { get; init; }

        [JsonPropertyName("LastAvailableChange")]
        public required uint LastAvailableChange { get; init; }

        [JsonPropertyName("LastChangeInSegment")]
        public required uint LastChangeInSegment { get; init; }
    }

    private sealed class PartyNotFoundException(Guid partyUuid)
        : InvalidOperationException($"Party {partyUuid} not found")
    {
        /// <summary>
        /// Gets the UUID of the party that was not found.
        /// </summary>
        public Guid PartyUuid => partyUuid;
    }
}
