namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// XML field name and string value extracted from CCR XML input.
/// </summary>
public class CcrField
{
    /// <summary>
    /// Gets or sets the name of the field as defined in the CCR XML schema.
    /// </summary>
    public FieldName FieldName { get; set; }

    /// <summary>
    /// Gets or sets the value of the field as extracted from the CCR XML data.
    /// </summary>
    public required string Value { get; set; }
}

/// <summary>
/// The set of field names defined in the CCR XML schema, representing various attributes of a party entity such as name, address, organization number, etc.
/// </summary>
public enum FieldName
{
    /// <summary>
    /// The orginal name in a changed
    /// </summary>
    NAVN1,

    /// <summary>
    /// The edited name in a change
    /// </summary>
    REDNAVN,

    /// <summary>
    /// Gets or sets the postal code associated with the address.
    /// </summary>
    POSTNR,

    /// <summary>
    /// Represents a country code.
    /// </summary>
    LANDKODE,

    /// <summary>
    /// The kommunenr (municipality number) associated with the address.
    /// </summary>
    KOMNR,

    /// <summary>
    /// The street part of the address. ( Usually the second line of the address, since the first is the entity/organisation name
    /// </summary>
    ADR2,

    /// <summary>
    /// Gets or sets the role's ssn identifier
    /// </summary>
    ROLLEFNR
}

/// <summary>
/// Provides extension methods for converting string representations of field names to their corresponding FieldName
/// enumeration values.
/// </summary>
/// <remarks>This class contains extension methods that simplify working with field names represented as strings
/// by enabling conversion to the strongly-typed FieldName enum. These methods are intended to improve code readability
/// and reduce errors when handling field names in string form.</remarks>
public static class FieldNameExtensions
{
    /// <summary>
    /// Converts a string representation of a field name to the corresponding <see cref="FieldName"/> enum value.
    /// </summary>
    /// <param name="fieldName">The string representation of the field name.</param>
    /// <returns>The corresponding <see cref="FieldName"/> enum value.</returns>
    /// <exception cref="ArgumentException">Thrown when the input string does not match any defined field names.</exception>
    public static FieldName ToFieldName(this string fieldName)
    {
        return fieldName switch
        {
            "navn1" => FieldName.NAVN1,
            "rednavn" => FieldName.REDNAVN,
            "postnr" => FieldName.POSTNR,
            "landkode" => FieldName.LANDKODE,
            "kommunenr" => FieldName.KOMNR,
            "adresse2" => FieldName.ADR2,
            "rolleFoedselsnr" => FieldName.ROLLEFNR,
            _ => throw new ArgumentException($"Invalid field name: {fieldName}")
        };
    }
}
