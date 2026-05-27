using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ModelUtils;
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
    /// Maximum tolerated drift between SIRE's clock and ours before a non-null
    /// <c>opphoerstidspunkt</c> in the future is treated as bad data.
    /// </summary>
    private static readonly TimeSpan ValidToFutureGrace = TimeSpan.FromMinutes(10);

    /// <summary>
    /// SIRE's <c>opphoerstidspunkt</c> is treated as binary: <see langword="null"/> means
    /// "still in force", any value means "terminated, ignore this entry". We don't carry
    /// future-dated terminations forward — by the time the date arrives we will have
    /// re-fetched the document. If SIRE does send a <c>validTo</c> sitting meaningfully
    /// in the future, that's data we don't know how to safely process, so we add a
    /// validation error rather than silently picking an interpretation. The
    /// <see cref="ValidToFutureGrace"/> window absorbs ordinary clock skew between SIRE
    /// and us so a few-minute drift doesn't trip the check.
    /// </summary>
    private static void ValidateValidToNotFarFuture(
        ref ValidationContext context,
        DateTimeOffset? validTo,
        DateTimeOffset now,
        string path)
    {
        if (validTo is { } endsAt && endsAt > now + ValidToFutureGrace)
        {
            context.AddChildProblem(ValidationErrors.InvalidValue, path);
        }
    }

    /// <inheritdoc/>
    public bool TryValidate(
        ref ValidationContext context,
        OrganizationDocument input,
        [NotNullWhen(true)] out SireOrganization? validated)
    {
        OrganizationIdentifier? orgId = null;
        if (string.IsNullOrWhiteSpace(input.Identifier))
        {
            context.AddChildProblem(StdValidationErrors.Required, "/identifikator");
        }
        else if (!OrganizationIdentifier.TryParse(input.Identifier, provider: null, out var parsedOrgId))
        {
            context.AddChildProblem(ValidationErrors.InvalidOrganizationNumber, "/identifikator");
        }
        else
        {
            orgId = parsedOrgId;
        }

        // Map organisasjonsform via the validator so unknown values aggregate as errors
        // at "/organisasjonsform" rather than throwing. Missing/empty input yields the
        // default SL-code and succeeds.
        context.TryValidateChild(
            path: "/organisasjonsform",
            input.OrganizationForm,
            default(SireOrganizationFormMapper),
            out string? unitType);

        var now = _timeProvider.GetUtcNow();

        // Refuse to carry forward any future-dated termination on the postal address —
        // see ValidateValidToNotFarFuture for the rationale. NormalizeAddress treats
        // any non-null opphoerstidspunkt as "no current address" regardless of date.
        ValidateValidToNotFarFuture(ref context, input.PostalAddress?.ValidTo, now, "/postadresse/opphoerstidspunkt");

        // Validate business relationships up-front so any per-relationship problems
        // are added to the context before we decide whether to bail out.
        var businessRelationships = MapBusinessRelationships(
            ref context,
            input.BusinessRelationships,
            now);

        if (context.HasErrors)
        {
            validated = null;
            return false;
        }

        Debug.Assert(orgId is not null);
        Debug.Assert(unitType is not null);

        bool isDeleted = !string.IsNullOrWhiteSpace(input.DeletedDate);
        MailingAddressRecord? mailingAddress = NormalizeAddress(input.PostalAddress);

        validated = new SireOrganization
        {
            OrganizationIdentifier = orgId!,
            Name = input.CompanyName,
            UnitType = unitType,
            UnitStatus = isDeleted ? "S" : "E",
            IsDeleted = isDeleted,
            MailingAddress = mailingAddress,
            LastUpdated = input.PostalAddress?.UpdatedAt,
            BusinessRelationships = businessRelationships,
        };

        return true;
    }

    private MailingAddressRecord? NormalizeAddress(PostalAddress? postalAddress)
    {
        if (postalAddress is null)
        {
            return null;
        }

        // Any non-null opphoerstidspunkt means SIRE has marked this address as
        // terminated; treat it as no current address. Downstream consumers already
        // handle MailingAddress = null, and a stale address is worse than none for
        // mail delivery. (TryValidate has already added an error if the date sits
        // unreasonably far in the future — see ValidateValidToNotFarFuture.)
        if (postalAddress.ValidTo is not null)
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

    private static ImmutableValueArray<SireBusinessRelationship> MapBusinessRelationships(
        ref ValidationContext context,
        IReadOnlyList<BusinessRelationship>? relationships,
        DateTimeOffset now)
    {
        if (relationships is null or { Count: 0 })
        {
            return [];
        }

        var result = ImmutableArray.CreateBuilder<SireBusinessRelationship>(relationships.Count);
        for (var i = 0; i < relationships.Count; i++)
        {
            var rel = relationships[i];
            var path = $"/virksomhetsrelasjon/{i}";

            if (string.IsNullOrWhiteSpace(rel.RelationshipType))
            {
                context.AddChildProblem(StdValidationErrors.Required, path + "/relasjonstype");
                continue;
            }

            // Any non-null opphoerstidspunkt means SIRE has terminated this relationship;
            // skip it. Far-future validTo values are flagged as bad data — see
            // ValidateValidToNotFarFuture for the rationale. Full-upsert downstream clears
            // any previously-known assignments not present in this fresh document.
            if (rel.ValidTo is not null)
            {
                ValidateValidToNotFarFuture(ref context, rel.ValidTo, now, path + "/opphoerstidspunkt");
                continue;
            }

            var relatedId = rel.RelatedIdentifier;
            if (relatedId is null || string.IsNullOrWhiteSpace(relatedId.Value))
            {
                context.AddChildProblem(StdValidationErrors.Required, path + "/relatertIdentifikator");
                continue;
            }

            if (!string.Equals(relatedId.IdentifierType, "taxIdentificationNumber", StringComparison.Ordinal))
            {
                context.AddChildProblem(
                    ValidationErrors.InvalidValue,
                    path + "/relatertIdentifikator/identifikatortype");
                continue;
            }

            PersonIdentifier? personId = null;
            OrganizationIdentifier? orgId = null;
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
                context.AddChildProblem(
                    ValidationErrors.InvalidValue,
                    path + "/relatertIdentifikator/verdi");
                continue;
            }

            // Map relasjonstype via the validator so an unknown value adds an aggregated
            // error at "/virksomhetsrelasjon/{i}/relasjonstype" instead of throwing — this
            // lets a single SIRE response surface every unrecognised relasjonstype at once.
            if (!context.TryValidateChild(
                path: path + "/relasjonstype",
                rel.RelationshipType,
                default(SireRoleMapper),
                out string? roleIdentifier))
            {
                continue;
            }

            result.Add(new SireBusinessRelationship
            {
                RoleIdentifier = roleIdentifier,
                RelatedPersonIdentifier = personId,
                RelatedOrganizationIdentifier = orgId,
            });
        }

        return result.DrainToImmutableValueArray();
    }
}
