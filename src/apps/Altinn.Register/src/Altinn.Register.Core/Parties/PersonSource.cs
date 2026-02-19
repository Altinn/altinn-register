namespace Altinn.Register.Core.Parties;

/// <summary>
/// Specifies the source of a person-party.
/// </summary>
public enum PersonSource
    : byte
{
    /// <summary>
    /// The Norwegian National Population Register.
    /// </summary>
    NationalPopulationRegister = 1,
}
