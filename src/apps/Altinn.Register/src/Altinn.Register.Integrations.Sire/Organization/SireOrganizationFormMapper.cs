using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Integrations.Sire.Organization;

/// <summary>
/// Maps SIRE <c>organisasjonsform</c> technical names to Skattelisten (SL) codes.
/// </summary>
/// <remarks>
/// SIRE returns the technical name (camelCase, e.g. "indreSelskap") in
/// <c>organisasjonsform</c>. We persist the SL-code (e.g. "KS") on
/// <c>SireOrganization.UnitType</c> to stay consistent with how organization forms from
/// other sources (CCR's "AS", "ENK", "NUF", …) are stored.
/// </remarks>
public static class SireOrganizationFormMapper
{
    /// <summary>
    /// Default SL-code applied when SIRE omits the <c>organisasjonsform</c> field. <c>IS</c>
    /// (<c>indreSelskap</c>) — the most common entity form.
    /// </summary>
    public const string DefaultOrganizationForm = "IS"; // Skatt checks and comes back on default value as there is no value avaialble currently

    /// <summary>
    /// Maps a SIRE <c>organisasjonsform</c> technical name to its SL-code (or applies the
    /// default when the input is missing).
    /// </summary>
    /// <param name="organisasjonsform">The camelCase technical name from SIRE</param>
    /// <returns>The matching SL-code (e.g. "IS"); <see cref="DefaultOrganizationForm"/> if input was missing.</returns>
    /// <exception cref="ArgumentException">Thrown when a non-empty value is not in the kodeliste.</exception>
    public static string GetOrganizationFormOrDefault(string? organisasjonsform)
        => string.IsNullOrWhiteSpace(organisasjonsform)
            ? DefaultOrganizationForm
            : GetOrganizationForm(organisasjonsform);

    /// <summary>
    /// Maps a SIRE <c>organisasjonsform</c> technical name to its SL-code.
    /// </summary>
    /// <param name="organisasjonsform">The camelCase technical name from SIRE (e.g. "kommandittselskap").</param>
    /// <returns>The matching SL-code (e.g. "KS").</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not in the kodeliste.</exception>
    public static string GetOrganizationForm(string organisasjonsform) => organisasjonsform switch
    {
        // Technical name (camelCase) -> SL-code.
        "ansvarligSelskapMedSolidariskAnsvar" => "ANS",
        "ansvarligSelskapMedDeltAnsvar" => "DA",
        "indreSelskap" => "IS",
        "kommandittselskap" => "KS",
        "norskkontrollertUtenlandskSelskap" => "NOKUS",
        "partrederi" => "PRE",
        "tingsrettsligSameie" => "SAM",
        "utenlandskAnsvarligSelskap" => "UTLANS",
        "virksomhetDrevetIFellesskap" => "VIFE",
        _ => ThrowHelper.ThrowArgumentException<string>(
            nameof(organisasjonsform),
            $"Unknown SIRE organisasjonsform: '{organisasjonsform}'."),
    };
}
