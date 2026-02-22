using System.Text.Json.Serialization;
using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Specifies the source of a person-party.
/// </summary>
[StringEnumConverter]
public enum PersonSource
    : byte
{
    /// <summary>
    /// The Norwegian National Population Register.
    /// </summary>
    [JsonStringEnumMemberName("npr")]
    NationalPopulationRegister = 1,
}
