namespace Altinn.Platform.Register.Models;

/// <summary>
/// Specifies the components that should be included when retrieving party's information.
/// </summary>
[Flags]
public enum PartyComponentOption : uint
{
    /// <summary>
    /// No additional components are included.
    /// </summary>
    None = 0,

    /// <summary>
    /// Includes the party's first name, middle name, and last name.
    /// </summary>
    NameComponents = 1 << 0,
}
