namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Infotype definition for CCR (Customer Contact Register) data.
/// This class represents the structure of an infotye as defined in the CCR XML schema.
/// </summary>
public class CcrInfoType
{
    /// <summary>
    /// Gets or sets the type of felt used, such as "NAVN", "FADR", "INNH" etc...
    /// </summary>
    public required string FeltType { get; set; }

    /// <summary>
    /// Gets or sets the type of change represented by this instance. Such as "N"
    /// </summary>
    public required string EndringsType { get; set; }

    /// <summary>
    /// Gets or sets the collection of fields included in this node
    /// </summary>
    public required List<CcrField> Fields { get; set; }
}
