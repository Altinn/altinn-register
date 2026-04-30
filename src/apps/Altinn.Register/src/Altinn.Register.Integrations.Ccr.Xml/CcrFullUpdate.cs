namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Represents a full update to a Norwegian Central Coordinating Register for Legal Entities (CCR) (Enhetsregisteret ER) party, including all relevant party information.
/// </summary>
/// <remarks> Use this class when a complete replacement of the party's data is required, rather than a partial update. Inherits from CcrPartyUpdate, which provides base update functionality.</remarks>
public sealed class CcrFullUpdate
    : CcrPartyUpdate
{
    /// <summary>
    /// Gets or sets the organisation number.
    /// </summary>
    public required string Organisasjonsnummer { get; set; }

    /// <summary>
    /// Gets or sets the organisation form.
    /// </summary>
    public required string Organisasjonsform { get; set; }

    /// <summary>
    /// Gets or sets the main case type.
    /// </summary>
    public required string Hovedsakstype { get; set; }

    /// <summary>
    /// Gets or sets the sub case type.
    /// </summary>
    public required string Undersakstype { get; set; }

    /// <summary>
    /// Gets or sets the first transfer date.
    /// </summary>
    public required DateTimeOffset FoersteOverfoering { get; set; }

    /// <summary>
    /// Gets or sets the date of birth.
    /// </summary>
    public required DateTimeOffset DatoFoedt { get; set; }

    /// <summary>
    /// Gets or sets the date last changed.
    /// </summary>
    public required DateTimeOffset DatoSistEndret { get; set; }

    /// <summary>
    /// Gets or sets the info types.
    /// </summary>
    public List<CcrInfoType> Infotypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the co-changes.
    /// </summary>
    public List<CcrSamendring> Samendringer { get; set; } = [];
}
