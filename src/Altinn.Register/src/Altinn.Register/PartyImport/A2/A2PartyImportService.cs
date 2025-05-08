#nullable enable

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using CommunityToolkit.Diagnostics;
using V1Models = Altinn.Platform.Register.Models;
using V1PartyType = Altinn.Platform.Register.Enums.PartyType;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Implementation of <see cref="IA2PartyImportService"/>.
/// </summary>
internal sealed partial class A2PartyImportService
    : IA2PartyImportService
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<A2PartyImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportService"/> class.
    /// </summary>
    public A2PartyImportService(
        HttpClient httpClient,
        TimeProvider timeProvider,
        ILogger<A2PartyImportService> logger)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<A2PartyChangePage> GetChanges(
        uint fromExclusive = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        PartyChangesResponse response;
        while (true)
        {
            response = await GetChangesPage(fromExclusive, cancellationToken);

            if (response.PartyChangeList.Count == 0)
            {
                break;
            }

            yield return MapChangePage(response);

            Assert(response.LastChangeInSegment > fromExclusive);
            fromExclusive = response.LastChangeInSegment;
        }
    }

    /// <inheritdoc />
    public async Task<Result<PartyRecord>> GetParty(Guid partyUuid, CancellationToken cancellationToken = default)
    {
        var url = $"register/api/parties?ignoreCache=true&partyuuid={partyUuid}";

        using var responseMessage = await _httpClient.GetAsync(url, cancellationToken);
        if (responseMessage.StatusCode == HttpStatusCode.Gone)
        {
            return Problems.PartyGone.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        if (responseMessage.StatusCode == HttpStatusCode.NotFound)
        {
            return Problems.PartyNotFound.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        if (!responseMessage.IsSuccessStatusCode)
        {
            return Problems.PartyFetchFailed.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        var response = await responseMessage.Content.ReadFromJsonAsync<V1Models.Party>(_options, cancellationToken);
        if (response is null)
        {
            return Problems.PartyFetchFailed.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        Assert(response.PartyUuid == partyUuid);
        return MapParty(response);
    }

    /// <inheritdoc />
    public Task<Result<PartyUserRecord>> GetOrCreatePersonUser(Guid partyUuid, CancellationToken cancellationToken = default)
    {
        var url = $"profile/api/users/getorcreate/{partyUuid}";

        return GetPartyUser(url, partyUuid, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<PartyUserRecord>> GetPartyUser(Guid partyUuid, CancellationToken cancellationToken = default)
    {
        var url = $"profile/api/users?userUUID={partyUuid}";

        return GetPartyUser(url, partyUuid, cancellationToken);
    }

    private async Task<Result<PartyUserRecord>> GetPartyUser(
        string url,
        Guid partyUuid,
        CancellationToken cancellationToken)
    {
        using var responseMessage = await _httpClient.GetAsync(url, cancellationToken);
        if (responseMessage.StatusCode == HttpStatusCode.Gone)
        {
            return Problems.PartyGone.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        if (responseMessage.StatusCode == HttpStatusCode.NotFound)
        {
            return Problems.PartyNotFound.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        if (!responseMessage.IsSuccessStatusCode)
        {
            return Problems.PartyFetchFailed.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        PartyProfile? response;
        var contentAsString = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            response = JsonSerializer.Deserialize<PartyProfile>(contentAsString, _options);
        }
        catch (JsonException ex)
        {
            Log.FailedToDeserializePartyProfile(_logger, url, ex);
            return Problems.PartyFetchFailed.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }
        
        if (response is null)
        {
            return Problems.PartyFetchFailed.Create([
                new("partyUuid", partyUuid.ToString()),
            ]);
        }

        Assert(response.UserUuid == partyUuid);
        Assert(response.Party.PartyUuid == partyUuid);
        return MapPartyUser(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<A2PartyExternalRoleAssignment> GetExternalRoleAssignmentsFrom(
        uint fromPartyId,
        Guid fromPartyUuid,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = $"register/api/parties/partyroles/{fromPartyId}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);

        // TODO: remove once SBL bridge is fixed.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // SBL bridge return 404 if no role-assignments exist.
            yield break;
        }

        await foreach (var item in response.Content.ReadFromJsonAsAsyncEnumerable<PartyRoleAssignmentItem>(_options, cancellationToken))
        {
            if (item is null)
            {
                ThrowHelper.ThrowInvalidOperationException("Failed to parse party role assignment.");
            }

            var assignment = new A2PartyExternalRoleAssignment
            {
                ToPartyUuid = item.ToPartyUuid,
                RoleCode = item.RoleCode,
            };

            yield return assignment;

            switch (item.RoleCode)
            {
                case "KOMK":
                case "SREVA":
                case "KNUF":
                case "KEMN":
                    assignment = assignment with { RoleCode = "KONT" };
                    yield return assignment;
                    break;
            }
        }
    }

    private async Task<PartyChangesResponse> GetChangesPage(uint fromExclusive, CancellationToken cancellationToken)
    {
        var url = $"register/api/parties/partychanges/{fromExclusive}";

        Log.FetchingPartyChangesPage(_logger, fromExclusive);
        var response = await _httpClient.GetFromJsonAsync<PartyChangesResponse>(url, _options, cancellationToken);
        if (response is null)
        {
            throw new InvalidOperationException("Failed to parse party changes.");
        }

        return response;
    }

    private PartyUserRecord MapPartyUser(PartyProfile profile)
    {
        var userId = checked((uint)profile.UserId);

        return new PartyUserRecord { UserIds = ImmutableValueArray.Create(userId) };
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
            V1PartyType.SelfIdentified => MapSelfIdentifiedUser(party, _timeProvider.GetUtcNow()),
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

        static DateOnly? CalculateDateOfBirth(PersonIdentifier personIdentifier)
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

            try
            {
                return new DateOnly(year, monthComponent, dayComponent);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Invalid date of birth
                return null;
            }
        }

        static SelfIdentifiedUserRecord MapSelfIdentifiedUser(V1Models.Party party, DateTimeOffset now)
        {
            var partyUuid = party.PartyUuid!.Value;
            var partyId = checked((uint)party.PartyId);
            var displayName = Normalize(party.Name);

            if (displayName is null)
            {
                displayName = "Selvidentifisert bruker uten navn";
            }

            return new SelfIdentifiedUserRecord
            {
                // party fields
                PartyUuid = partyUuid,
                PartyId = partyId,
                DisplayName = displayName,
                PersonIdentifier = null,
                OrganizationIdentifier = null,
                CreatedAt = now,
                ModifiedAt = now,
                IsDeleted = party.IsDeleted,
                User = FieldValue.Unset,
                VersionId = FieldValue.Unset,
            };
        }

        static PersonRecord MapPerson(V1Models.Party party, DateTimeOffset now)
        {
            var person = party.Person!;

            var partyUuid = party.PartyUuid!.Value;
            var partyId = checked((uint)party.PartyId);
            var personIdentifier = MapPersonIdentifier(person.SSN.AsSpan().Trim());
            var firstName = Normalize(person.FirstName);
            var middleName = Normalize(person.MiddleName);
            var lastName = Normalize(person.LastName);
            var shortName = Normalize(person.Name);
            var displayName = MapPersonName(
                firstName: firstName,
                middleName: middleName,
                lastName: lastName,
                shortName: shortName);
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

            DateOnly? dateOfBirth = CalculateDateOfBirth(personIdentifier);
            DateOnly? dateOfDeath = person.DateOfDeath is null ? null : DateOnly.FromDateTime(person.DateOfDeath.Value);

            if (displayName is null && lastName is not null)
            {
                if (firstName is null)
                {
                    displayName = lastName;
                    firstName = "Mangler";
                }
                else
                {
                    displayName = $"{firstName} {lastName}";
                }

                if (middleName is not null)
                {
                    displayName += $" {middleName}";
                }
            }
            else if (firstName is null && lastName is null && middleName is null && displayName is null)
            {
                firstName = "Mangler";
                lastName = "Navn";
                displayName = "Mangler Navn";
            }
            else if (firstName is null && lastName is null && displayName is not null)
            {
                if (IsSyntheticImportName(displayName))
                {
                    firstName = "Mangler";
                    lastName = "Navn";
                    displayName = "Mangler Navn";
                }
                else
                {
                    var components = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    lastName = components.Length > 0 ? components[0] : "Navn";
                    firstName = components.Length > 1 ? components[1] : "Mangler";
                    displayName = $"{firstName} {lastName}";
                }
            }

            return new PersonRecord
            {
                // party fields
                PartyUuid = partyUuid,
                PartyId = partyId,
                DisplayName = displayName,
                PersonIdentifier = personIdentifier,
                OrganizationIdentifier = null,
                CreatedAt = now,
                ModifiedAt = now,
                IsDeleted = isDeleted,
                User = FieldValue.Unset,
                VersionId = FieldValue.Unset,

                // person fields
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName,
                ShortName = shortName ?? displayName,
                Address = address,
                MailingAddress = mailingAddress,
                DateOfBirth = FieldValue.From(dateOfBirth),
                DateOfDeath = FieldValue.From(dateOfDeath),
            };
        }

        static string? MapPersonName(
            string? firstName,
            string? middleName,
            string? lastName,
            string? shortName)
        {
            if (firstName is null && lastName is null)
            {
                return shortName;
            }

            var builder = new StringBuilder((firstName?.Length ?? 0) + (middleName?.Length ?? 0) + (lastName?.Length ?? 0) + 3);
            if (firstName is not null)
            {
                builder.Append(firstName);
                builder.Append(' ');
            }

            if (middleName is not null)
            {
                builder.Append(middleName);
                builder.Append(' ');
            }

            if (lastName is not null)
            {
                builder.Append(lastName);
                builder.Append(' ');
            }

            builder.Length--; // remove trailing space
            return builder.ToString();
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
            var partyId = checked((uint)party.PartyId);
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

            if (string.IsNullOrEmpty(name))
            {
                name = "Organisasjon med manglende navn";
            }

            return new OrganizationRecord
            {
                // party fields
                PartyUuid = partyUuid,
                PartyId = partyId,
                DisplayName = name,
                PersonIdentifier = null,
                OrganizationIdentifier = organizationNumber,
                CreatedAt = now,
                ModifiedAt = now,
                IsDeleted = isDeleted,
                User = FieldValue.Unset,
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

        static bool IsSyntheticImportName(string name)
            => string.Equals(name, "Inserted By ER Import", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Ikke i Altinn register", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Inserted By FReg Import", StringComparison.OrdinalIgnoreCase);
    }

    private static void Assert([DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression(nameof(condition))] string? conditionString = null)
    {
        if (!condition)
        {
            ThrowHelper.ThrowInvalidOperationException($"Assertion failed: {conditionString}");
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
    /// A2 party role assignment model.
    /// </summary>
    internal sealed class PartyRoleAssignmentItem
    {
        /// <summary>
        /// Gets the party id of the party that the role is assigned to.
        /// </summary>
        [JsonPropertyName("PartyId")]
        public required int ToPartyId { get; init; }

        /// <summary>
        /// Gets the party uuid of the party that the role is assigned to.
        /// </summary>
        [JsonPropertyName("PartyUuid")]
        public required Guid ToPartyUuid { get; init; }

        /// <summary>
        /// Gets the kind of assignment.
        /// </summary>
        [JsonPropertyName("PartyRelation")]
        public required string PartyRelation { get; init; }

        /// <summary>
        /// Gets the role code of the role that is assigned.
        /// </summary>
        [JsonPropertyName("RoleCode")]
        public required string RoleCode { get; init; }
    }

    /// <summary>
    /// Partial A2 party profile model.
    /// </summary>
    internal sealed class PartyProfile
    {
        /// <summary>
        /// Gets the user id.
        /// </summary>
        [JsonPropertyName("UserId")]
        public required int UserId { get; init; }

        /// <summary>
        /// Gets the user UUID (should be the same as the party UUID).
        /// </summary>
        [JsonPropertyName("UserUUID")]
        public required Guid UserUuid { get; init; }

        /// <summary>
        /// Gets the party object.
        /// </summary>
        [JsonPropertyName("Party")]
        public required PartyProfileParty Party { get; init; }
    }

    /// <summary>
    /// Partial A2 party model.
    /// </summary>
    internal sealed class PartyProfileParty
    {
        /// <summary>
        /// Gets the party UUID.
        /// </summary>
        [JsonPropertyName("PartyUUID")]
        public required Guid PartyUuid { get; init; }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Fetching party changes from {FromExclusive}.")]
        public static partial void FetchingPartyChangesPage(ILogger logger, uint fromExclusive);

        [LoggerMessage(2, LogLevel.Error, "Failed to deserialize party profile from {Url}.")]
        public static partial void FailedToDeserializePartyProfile(ILogger logger, string url, Exception exception);
    }
}
