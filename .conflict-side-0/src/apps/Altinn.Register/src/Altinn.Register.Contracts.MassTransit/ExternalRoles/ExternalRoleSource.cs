using System.Text.Json.Serialization;

namespace Altinn.Register.Contracts.ExternalRoles;

/// <summary>
/// The source of an external role.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExternalRoleSource>))]
public enum ExternalRoleSource
{
    /// <summary>
    /// The Norwegian Central Coordinating Register for Legal Entities.
    /// </summary>
    [JsonStringEnumMemberName("ccr")]
    CentralCoordinatingRegister,

    /// <summary>
    /// The Norwegian National Population Register.
    /// </summary>
    [JsonStringEnumMemberName("npr")]
    NationalPopulationRegister,

    /// <summary>
    /// The Norwegian register of employers and employees.
    /// </summary>
    [JsonStringEnumMemberName("aar")]
    EmployersEmployeeRegister,
}
