using Altinn.Authorization.ModelUtils;

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
    /// The following list of fields are for ROLLE type "R"
    /// </summary>
    public FieldValue<string> RolleAnsvarsandel { get; set; }

    /// <summary>
    /// Gets or sets the date on which the role was resigned or ended.
    /// </summary>
    public FieldValue<string> RolleFratraadt { get; set; }

    /// <summary>
    /// Gets or sets the selected role value.
    /// </summary>
    public FieldValue<string> RolleValgtav { get; set; }

    /// <summary>
    /// Gets or sets the role sequence value.
    /// </summary>
    public FieldValue<string> RolleRekkefoelge { get; set; }

    /// <summary>
    /// Gets or sets the national identification number associated with the role.
    /// </summary>
    public FieldValue<string> RolleFoedselsnr { get; set; }

    /// <summary>
    /// Gets or sets the first name value associated with the field.
    /// </summary>
    public FieldValue<string> Fornavn { get; set; }

    /// <summary>
    /// Gets or sets the middle name associated with the entity.
    /// </summary>
    public FieldValue<string> Mellomnavn { get; set; }

    /// <summary>
    /// Gets or sets the family name associated with the entity.
    /// </summary>
    public FieldValue<string> Slektsnavn { get; set; }

    /// <summary>
    /// Gets or sets the postal code associated with the address.
    /// </summary>
    public FieldValue<string> Postnr { get; set; }

    /// <summary>
    /// Gets or sets the first line of the address.
    /// </summary>
    public FieldValue<string> Adresse1 { get; set; }

    /// <summary>
    /// Gets or sets the second line of the address, such as an apartment, suite, or building information.
    /// </summary>
    public FieldValue<string> Adresse2 { get; set; }

    /// <summary>
    /// Gets or sets the third line of the address.
    /// </summary>
    public FieldValue<string> Adresse3 { get; set; }

    /// <summary>
    /// Gets or sets the country code associated with the address.
    /// </summary>
    /// <remarks>The country code should be provided as a valid ISO 3166-1 alpha-2 or alpha-3 code, depending
    /// on system requirements. This property is typically used to identify the country for postal or regulatory
    /// purposes.</remarks>
    public FieldValue<string> AdresseLandkode { get; set; }

    /// <summary>
    /// Gets or sets the status of the person as a string value.
    /// </summary>
    public FieldValue<string> Personstatus { get; set; }

    /// <summary>
    /// Gets or sets the responsibility share associated with the connection.
    /// </summary>
    public FieldValue<string> KnytningAnsvarsandel { get; set; }

    /// <summary>
    /// Gets or sets the date when the association was terminated.
    /// </summary>
    public FieldValue<string> KnytningFratraadt { get; set; }

    /// <summary>
    /// Gets or sets the organization number associated with the connection.
    /// </summary>
    public FieldValue<string> KnytningOrganisasjonsnummer { get; set; }

    /// <summary>
    /// Gets or sets the selected value for the 'KnytningValgtav' field.
    /// </summary>
    public FieldValue<string> KnytningValgtav { get; set; }

    /// <summary>
    /// Gets or sets the sequence number used to determine the order of association.
    /// </summary>
    public FieldValue<string> KnytningRekkefoelge { get; set; }

    /// <summary>
    /// Gets or sets the validated organization number associated with the field.
    /// </summary>
    public FieldValue<string> KorrektOrganisasjonsnummer { get; set; }
}
