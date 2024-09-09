namespace Altinn.Register.Core.Parties;

/// <summary>
/// Represents a person.
/// </summary>
public sealed record Person
    : Party
{
    /// <summary>
    /// Initializes a new <see cref="Person"/>.
    /// </summary>
    public Person()
        : base(PartyType.Person)
    {
    }

    /// <summary>
    /// Gets the first name.
    /// </summary>
    public required string FirstName { get; init; }

    /// <summary>
    /// Gets the (optional) middle name.
    /// </summary>
    public required string? MiddleName { get; init; }

    /// <summary>
    /// Gets the last name.
    /// </summary>
    public required string LastName { get; init; }

    /// <summary>
    /// Gets the (optional) <see cref="StreetAddress"/> of the person.
    /// </summary>
    public required StreetAddress? Address { get; init; }

    /// <summary>
    /// Gets the (optional) <see cref="Parties.MailingAddress"/> of the person.
    /// </summary>
    public required MailingAddress? MailingAddress { get; init; }

    /// <summary>
    /// Gets the date of birth of the person.
    /// </summary>
    public required DateOnly DateOfBirth { get; init; }

    /// <summary>
    /// Gets the (optional) date of death of the person.
    /// </summary>
    public required DateOnly? DateOfDeath { get; init; }
}
