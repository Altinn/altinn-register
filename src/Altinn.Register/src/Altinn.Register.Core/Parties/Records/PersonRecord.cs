using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// A database record for a person.
/// </summary>
[PolymorphicFieldValueRecord]
public sealed record PersonRecord
    : PartyRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersonRecord"/> class.
    /// </summary>
    public PersonRecord()
        : base(Parties.PartyType.Person)
    {
    }

    /// <summary>
    /// Gets the first name.
    /// </summary>
    public required FieldValue<string> FirstName { get; init; }

    /// <summary>
    /// Gets the (optional) middle name.
    /// </summary>
    public required FieldValue<string> MiddleName { get; init; }

    /// <summary>
    /// Gets the last name.
    /// </summary>
    public required FieldValue<string> LastName { get; init; }

    /// <summary>
    /// Gets the short name.
    /// </summary>
    public required FieldValue<string> ShortName { get; init; }

    /// <summary>
    /// Gets the (optional) <see cref="StreetAddress"/> of the person.
    /// </summary>
    public required FieldValue<StreetAddress> Address { get; init; }

    /// <summary>
    /// Gets the (optional) <see cref="Parties.MailingAddress"/> of the person.
    /// </summary>
    public required FieldValue<MailingAddress> MailingAddress { get; init; }

    /// <summary>
    /// Gets the date of birth of the person.
    /// </summary>
    public required FieldValue<DateOnly> DateOfBirth { get; init; }

    /// <summary>
    /// Gets the (optional) date of death of the person.
    /// </summary>
    public required FieldValue<DateOnly> DateOfDeath { get; init; }
}
