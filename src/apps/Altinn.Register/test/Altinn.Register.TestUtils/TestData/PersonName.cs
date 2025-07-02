using System.Text;

namespace Altinn.Register.TestUtils.TestData;

/// <summary>
/// Represents a person name.
/// </summary>
public sealed class PersonName
{
    /// <summary>
    /// Creates a new instance of <see cref="PersonName"/>.
    /// </summary>
    /// <param name="firstName">The persons first name.</param>
    /// <param name="lastName">The persons last name.</param>
    /// <returns>A <see cref="PersonName"/>.</returns>
    public static PersonName Create(string firstName, string lastName)
        => Create(firstName, null, lastName);

    /// <summary>
    /// Creates a new instance of <see cref="PersonName"/>.
    /// </summary>
    /// <param name="firstName">The persons first name.</param>
    /// <param name="middleName">The persons middle name.</param>
    /// <param name="lastName">The persons last name.</param>
    /// <returns>A <see cref="PersonName"/>.</returns>
    public static PersonName Create(string firstName, string? middleName, string lastName)
    {
        var sb = new StringBuilder(lastName);
        sb.Append(' ');
        sb.Append(firstName);

        if (!string.IsNullOrEmpty(middleName))
        {
            sb.Append(' ');
            sb.Append(middleName);
        }

        var displayName = sb.ToString();
        sb.Length = Math.Min(sb.Length, 40);
        var shortName = sb.ToString();

        return new PersonName(firstName, middleName, lastName, shortName, displayName);
    }

    /// <summary>
    /// Gets the first name.
    /// </summary>
    public string FirstName { get; }

    /// <summary>
    /// Gets the middle name.
    /// </summary>
    public string? MiddleName { get; }

    /// <summary>
    /// Gets the last name.
    /// </summary>
    public string LastName { get; }

    /// <summary>
    /// Gets the short name.
    /// </summary>
    public string ShortName { get; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; }

    private PersonName(string firstName, string? middleName, string lastName, string shortName, string displayName)
    {
        FirstName = firstName;
        MiddleName = middleName;
        LastName = lastName;
        ShortName = shortName;
        DisplayName = displayName;
    }
}
