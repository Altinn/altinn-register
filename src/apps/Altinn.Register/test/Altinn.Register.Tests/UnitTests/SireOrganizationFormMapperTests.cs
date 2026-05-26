using Altinn.Register.Integrations.Sire.Organization;

namespace Altinn.Register.Tests.UnitTests;

/// <summary>
/// Tests for <see cref="SireOrganizationFormMapper"/>.
/// </summary>
public class SireOrganizationFormMapperTests
{
    /// <summary>
    /// Every known SIRE <c>organisasjonsform</c> must map to its expected SL-code.
    /// </summary>
    [Theory]
    [MemberData(nameof(KnownOrganizationFormMappings))]
    public void GetOrganizationForm_MapsKnownValue(string organisasjonsform, string expectedSlCode)
    {
        var actual = SireOrganizationFormMapper.GetOrganizationForm(organisasjonsform);
        Assert.Equal(expectedSlCode, actual);
    }

    /// <summary>
    /// Anything outside the kodeliste throws — keeps us loud when Skatt adds a new form.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("AS")] // common Norwegian form, but not in this kodeliste
    [InlineData("notAForm")]
    [InlineData("KS")] // SL-code (output of the mapper, mistakenly fed back in)
    [InlineData("KOMMANDITTSELSKAP")] // case mismatch
    [InlineData("kommandittselskap ")] // trailing whitespace
    public void GetOrganizationForm_ThrowsForUnknown(string organisasjonsform)
    {
        Assert.Throws<ArgumentException>(() => SireOrganizationFormMapper.GetOrganizationForm(organisasjonsform));
    }

    /// <summary>
    /// Null input falls through to the throw branch.
    /// </summary>
    [Fact]
    public void GetOrganizationForm_NullInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => SireOrganizationFormMapper.GetOrganizationForm(null!));
    }

    /// <summary>
    /// Missing or whitespace input on the default-aware overload yields the documented default
    /// SL-code (<c>IS</c>), reflecting that SIRE often omits the field.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetOrganizationFormOrDefault_MissingInput_ReturnsDefault(string? organisasjonsform)
    {
        var actual = SireOrganizationFormMapper.GetOrganizationFormOrDefault(organisasjonsform);
        Assert.Equal(SireOrganizationFormMapper.DefaultOrganizationForm, actual);
        Assert.Equal("IS", actual);
    }

    /// <summary>
    /// When the value is present and known, the default-aware overload should map it to its
    /// SL-code the same way as <see cref="SireOrganizationFormMapper.GetOrganizationForm"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(KnownOrganizationFormMappings))]
    public void GetOrganizationFormOrDefault_MapsPresentKnownValue(string organisasjonsform, string expectedSlCode)
    {
        var actual = SireOrganizationFormMapper.GetOrganizationFormOrDefault(organisasjonsform);
        Assert.Equal(expectedSlCode, actual);
    }

    /// <summary>
    /// Unknown but non-empty input still throws on the default-aware overload — the default
    /// only applies to the missing case, never as a silent fallback for unrecognized values.
    /// </summary>
    [Fact]
    public void GetOrganizationFormOrDefault_UnknownNonEmptyValue_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => SireOrganizationFormMapper.GetOrganizationFormOrDefault("notARealForm"));
    }

    /// <summary>
    /// Authoritative table of the 9 SIRE organisasjonsform technical names → SL-codes from
    /// Skatteetaten's <c>organisasjonsformHovedenhetType.xml</c> kodeliste.
    /// </summary>
    public static TheoryData<string, string> KnownOrganizationFormMappings => new()
    {
        { "ansvarligSelskapMedSolidariskAnsvar", "ANS" },
        { "ansvarligSelskapMedDeltAnsvar",       "DA" },
        { "indreSelskap",                        "IS" },
        { "kommandittselskap",                   "KS" },
        { "norskkontrollertUtenlandskSelskap",   "NOKUS" },
        { "partrederi",                          "PRE" },
        { "tingsrettsligSameie",                 "SAM" },
        { "utenlandskAnsvarligSelskap",          "UTLANS" },
        { "virksomhetDrevetIFellesskap",         "VIFE" },
    };
}
