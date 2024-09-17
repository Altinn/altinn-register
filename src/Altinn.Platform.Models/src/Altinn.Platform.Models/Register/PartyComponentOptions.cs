namespace Altinn.Platform.Register.Enums;

/// <summary>
/// Represents options for including different components of a party.
/// </summary>
[Flags]
public enum PartyComponentOptions
{
    /// <summary>
    /// No extra components are included.
    /// </summary>
    None = 0,

    /// <summary>
    /// Include the party's first, middle, and last name.
    /// </summary>
    NameComponents = 1 << 0,
}
