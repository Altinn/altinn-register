namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Represents a change node in the CCR (Central Coordinating Register) data model, encapsulating the type of change,
/// the affected field type, and associated field values.
/// </summary>
/// <remarks>This class is typically used to model changes to roles or other entities as defined by the CCR XML
/// schema. Each instance describes a single change operation, including its type and the relevant fields. The structure
/// and meaning of the fields depend on the context and the CCR specification.</remarks>
public class CcrSamendring
{
    /// <summary>
    /// Gets or sets the type of felt used, for SamEndring it is usually the Role name from ER
    /// such as "INNH", "REGN" etc...
    /// </summary>
    public required string FeltType { get; set; }

    /// <summary>
    /// Gets or sets the type of change represented by this instance. Such as "N"
    /// </summary>
    public required string EndringsType { get; set; }

    /// <summary>
    /// Gets or sets the type identifier for the object. Such as "R"
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the data associated with this instance. Such as "D"
    /// </summary>
    public required string Data { get; set; }

    /// <summary>
    /// Gets or sets the collection of fields included in this node.
    /// For Role changes this will typically be FieldType.ROLLEFNR (which is the national identification number of the role holder),
    /// but for other change types it may include different fields as defined in the CCR XML schema.
    /// </summary>
    public required List<CcrField> Fields { get; set; } = [];
}
