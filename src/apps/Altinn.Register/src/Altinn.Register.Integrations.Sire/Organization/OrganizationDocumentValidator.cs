using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ProblemDetails.Validation;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Location;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Sire;

namespace Altinn.Register.Integrations.Sire.Organization;

/// <summary>
/// Validates and maps an <see cref="OrganizationDocument"/> to a <see cref="SireOrganization"/>.
/// </summary>
public sealed class OrganizationDocumentValidator
    : IValidator<OrganizationDocument, SireOrganization>
{
    private readonly ILocationLookup _lookup;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the OrganizationDocumentValidator class using the specified location lookup
    /// service and time source.
    /// </summary>
    /// <param name="lookup">The location lookup service used to validate organization document locations.</param>
    /// <param name="timeProvider">The clock used to filter out expired postadresse and virksomhetsrelasjon entries.</param>
    public OrganizationDocumentValidator(ILocationLookup lookup, TimeProvider timeProvider)
    {
        _lookup = lookup;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns true when the validTo indicates the
    /// entity is still currently valid as of now means
    /// "no termination set, still valid". A non-null value in the past means expired.
    /// </summary>
    private static bool IsStillValid(DateTimeOffset? validTo, DateTimeOffset now)
        => validTo is not { } endsAt || endsAt > now;

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        OrganizationDocument input,
        [NotNullWhen(true)] out SireOrganization? validated)
    {
        if (string.IsNullOrWhiteSpace(input.Identifier))
        {
            context.AddChildProblem(StdValidationErrors.Required, "/identifikator");
            validated = null;
            return false;
        }

        if (!OrganizationIdentifier.TryParse(input.Identifier, provider: null, out var orgId))
        {
            context.AddChildProblem(ValidationErrors.InvalidOrganizationNumber, "/identifikator");
            validated = null;
            return false;
        }

        bool isDeleted = !string.IsNullOrWhiteSpace(input.DeletedDate);
        MailingAddressRecord? mailingAddress = NormalizeAddress(input.PostalAddress);

        validated = new SireOrganization
        {
            OrganizationIdentifier = orgId,
            Name = input.CompanyName,
            UnitType = SireOrganizationFormMapper.GetOrganizationFormOrDefault(input.OrganizationForm),
            UnitStatus = isDeleted ? "S" : "E",
            IsDeleted = isDeleted,
            MailingAddress = mailingAddress,
            LastUpdated = input.PostalAddress?.UpdatedAt,
            BusinessRelationships = MapBusinessRelationships(input.BusinessRelationships, _timeProvider.GetUtcNow())
        };

        return true;
    }

    private MailingAddressRecord? NormalizeAddress(PostalAddress? postalAddress)
    {
        if (postalAddress is null)
        {
            return null;
        }

        // If the address itself has been terminated, treat it as no address. Downstream
        // consumers already handle MailingAddress = null, and a stale address is worse than
        // none for things like mail/letter delivery.
        if (!IsStillValid(postalAddress.ValidTo, _timeProvider.GetUtcNow()))
        {
            return null;
        }

        if (postalAddress.NorwegianAddress is { } norwegian)
        {
            return NormalizeNorwegianAddress(norwegian);
        }

        if (postalAddress.InternationalAddress is { } international)
        {
            return NormalizeInternationalAddress(international);
        }

        return null;
    }

    private static MailingAddressRecord? NormalizeNorwegianAddress(NorwegianAddress address)
    {
        var lines = NormalizeAddressLines(address.AddressLines, isForeign: false);
        var postalCode = NormalizeNorwegianPostalCode(address.PostalCode);
        var city = address.City?.Trim();

        if (lines.Count == 0 && postalCode is null && city is null)
        {
            return null;
        }

        return new MailingAddressRecord
        {
            Address = lines.Count > 0 ? string.Join(' ', lines) : null,
            PostalCode = postalCode,
            City = city,
        };
    }

    private MailingAddressRecord? NormalizeInternationalAddress(InternationalAddress address)
    {
        var lines = NormalizeAddressLines(address.AddressLines, isForeign: true);
        var postalCode = GetInternationalPostalCode(address.PostalCode, address.CountryCode);
        var city = address.City?.Trim();

        // Add country name if not already present in address lines
        if (TryLookupCountryName(address.CountryCode, out var countryName))
        {
            if (!lines.Any(line => line.Contains(countryName, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(countryName);
            }
        }

        if (lines.Count == 0 && postalCode is null && city is null)
        {
            return null;
        }

        return new MailingAddressRecord
        {
            Address = lines.Count > 0 ? string.Join(' ', lines) : null,
            PostalCode = postalCode,
            City = city,
        };
    }

    /// <summary>
    /// Normalizes address lines by stripping C/O prefixes and normalizing postbox prefixes.
    /// </summary>
    private static List<string> NormalizeAddressLines(IReadOnlyList<string>? addressLines, bool isForeign)
    {
        var result = new List<string>();
        if (addressLines is null)
        {
            return result;
        }

        foreach (var line in addressLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var normalized = RemoveCareOfPrefix(line);
            normalized = AddPostboxIfNeeded(normalized, isForeign);
            result.Add(normalized);
        }

        return result;
    }

    /// <summary>
    /// Pads Norwegian postal codes to 4 digits. Returns "0000" sentinel for missing/invalid codes.
    /// </summary>
    private static string? NormalizeNorwegianPostalCode(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        var trimmed = postalCode.Trim();
        if (!trimmed.All(char.IsDigit))
        {
            return null;
        }

        var padded = trimmed.PadLeft(4, '0');
        return padded == "0000" ? null : padded;
    }

    /// <summary>
    /// Formats international postal codes as "{COUNTRYCODE}-{number}" when country code is present and number is digits only.
    /// </summary>
    private static string? GetInternationalPostalCode(string? postalCode, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        var trimmed = postalCode.Trim();
        if (!string.IsNullOrWhiteSpace(countryCode) && trimmed.All(char.IsDigit))
        {
            return $"{countryCode.Trim().ToUpperInvariant()}-{trimmed}";
        }

        return trimmed;
    }

    private bool TryLookupCountryName(string? countryCode, [NotNullWhen(true)] out string? countryName)
    {
        if (string.IsNullOrEmpty(countryCode))
        {
            countryName = null;
            return false;
        }

        if (_lookup.TryGetCountry(countryCode, out var country))
        {
            countryName = country.Name;
            return true;
        }

        // fallback to the country code itself
        countryName = countryCode;
        return true;
    }

    private static readonly ImmutableArray<string> CareOfPrefixes
        = [
            "C/O ",
            "C/O: ",
            "C/O",
            "CO. ",
            "CO ",
            "CO/ ",
            "CO/",
            "C.O. ",
            "C.O.",
            "CO: ",
            "C\\O ",
            "C/0 ",
            "V/ ",
            "V/",
        ];

    private static string RemoveCareOfPrefix(string input)
    {
        foreach (var prefix in CareOfPrefixes)
        {
            if (input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return input[prefix.Length..].Trim();
            }
        }

        return input.Trim();
    }

    private static readonly ImmutableArray<string> PostBoxPrefixes
        = [
            "postboks",
            "postoffice box ",
            "P.O Box ",
            "P.O.Box ",
            "PO box ",
            "GPO Box ",
            "PO. box ",
            "postbox",
            "boks ",
            "box ",
            "pb.",
            "p.b.",
            "p. b.",
            "p b ",
        ];

    private static string AddPostboxIfNeeded(string input, bool isForeign)
    {
        foreach (var prefix in PostBoxPrefixes)
        {
            if (input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = input[prefix.Length..].Trim();
                var postBoxPrefix = isForeign ? "Postbox" : "Postboks";
                return $"{postBoxPrefix} {remainder}".Trim();
            }
        }

        return input;
    }

    private static IReadOnlyList<SireBusinessRelationship> MapBusinessRelationships(
        IReadOnlyList<BusinessRelationship>? relationships,
        DateTimeOffset now)
    {
        if (relationships is null or { Count: 0 })
        {
            return [];
        }

        var result = new List<SireBusinessRelationship>(relationships.Count);
        foreach (var rel in relationships)
        {
            if (string.IsNullOrWhiteSpace(rel.RelationshipType))
            {
                continue;
            }

            // Skip terminated relationships. We rely on full-upsert downstream to clear any
            // previously-known assignments not present in this fresh document.
            if (!IsStillValid(rel.ValidTo, now))
            {
                continue;
            }

            PersonIdentifier? personId = null;
            OrganizationIdentifier? orgId = null;

            // RelatedIdentifier is the integration project's model
            RelatedIdentifier? relatedId = rel.RelatedIdentifier;
            if (relatedId is { Value: not null })
            {
                if (relatedId.IdentifierType is not null && relatedId.IdentifierType == "taxIdentificationNumber")
                {
                    if (PersonIdentifier.TryParse(relatedId.Value, provider: null, out var parsedPerson))
                    {
                        personId = parsedPerson;
                    }
                    else if (OrganizationIdentifier.TryParse(relatedId.Value, provider: null, out var parsedOrg))
                    {
                        orgId = parsedOrg;
                    }
                    else
                    {
                        ////Should we log this as a warning that the identifier value could not be parsed as either person or organization identifier? And notify SKD about it 
                        ////or should we just ignore it since the identifier value is not valid according to our specifications for person and organization identifiers?
                    }
                }
                else
                {
                    ////SKD wants us to log any identifiertype that is not taxidentificationnumber and notify SKD about it because they mentioned that only taxidentificaitonnumber is expected here.
                    //// Find out how to log this in a good way and notify SKD about it.
                }
            }

            result.Add(new SireBusinessRelationship
            {
                RoleIdentifier = SireRoleMapper.GetRoleIdentifier(rel.RelationshipType),
                RelatedPersonIdentifier = personId,
                RelatedOrganizationIdentifier = orgId,
                ValidFrom = rel.ValidFrom,
                ValidTo = rel.ValidTo,
            });
        }

        return result;
    }
}
