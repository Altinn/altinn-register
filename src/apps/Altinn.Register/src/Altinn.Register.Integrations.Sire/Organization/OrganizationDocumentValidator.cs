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
internal sealed class OrganizationDocumentValidator
    : IValidator<OrganizationDocument, SireOrganization>
{
    private readonly ILocationLookup _lookup;

    /// <summary>
    /// Initializes a new instance of the OrganizationDocumentValidator class using the specified location lookup
    /// service.
    /// </summary>
    /// <param name="lookup">The location lookup service used to validate organization document locations. Cannot be null.</param>
    public OrganizationDocumentValidator(ILocationLookup lookup)
    {
        _lookup = lookup;
    }

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

        bool isDeleted = input.DeletedDate is not null;
        MailingAddressRecord? mailingAddress = NormalizeAddress(input.PostalAddress);

        validated = new SireOrganization
        {
            OrganizationIdentifier = orgId,
            Name = input.CompanyName,
            UnitType = input.OrganizationForm,
            UnitStatus = isDeleted ? "slettet" : null,
            IsDeleted = isDeleted,
            TaxLiabilityType = input.TaxLiabilityType,
            MailingAddress = mailingAddress,
            LastUpdated = input.PostalAddress?.UpdatedAt,
            BusinessRelationships = MapBusinessRelationships(input.BusinessRelationships)
        };

        return true;
    }

    private MailingAddressRecord? NormalizeAddress(PostalAddress? postalAddress)
    {
        if (postalAddress is null)
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
        var lines = NormalizeAddressLines(address.AddressLines);
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
        var lines = NormalizeAddressLines(address.AddressLines);
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
    private static List<string> NormalizeAddressLines(IReadOnlyList<string>? addressLines)
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
            normalized = AddPostboxIfNeeded(normalized, isForeign: false);
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
    IReadOnlyList<BusinessRelationship>? relationships)
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

            PersonIdentifier? personId = null;
            OrganizationIdentifier? orgId = null;

            // RelatedIdentifier is the integration project's model
            RelatedIdentifier? relatedId = rel.RelatedIdentifier;
            if (relatedId is { Value: not null })
            {
                var isNorwegian = string.Equals(relatedId.CountryCode, "NO", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(relatedId.CountryCode);

                if (isNorwegian)
                {
                    if (PersonIdentifier.TryParse(relatedId.Value, provider: null, out var parsedPerson))
                    {
                        personId = parsedPerson;
                    }
                    else if (OrganizationIdentifier.TryParse(relatedId.Value, provider: null, out var parsedOrg))
                    {
                        orgId = parsedOrg;
                    }
                }

                // Foreign identifiers — not supported in 1st iterasjon, skip
            }

            result.Add(new SireBusinessRelationship
            {
                RelationshipType = rel.RelationshipType,
                RelatedPersonIdentifier = personId,
                RelatedOrganizationIdentifier = orgId,
                ValidFrom = rel.ValidFrom,
                ValidTo = rel.ValidTo,
            });
        }

        return result;
    }
}
