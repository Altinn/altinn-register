using System.Text.Json.Serialization;
using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// Specifies the source of an organization-party.
/// </summary>
[StringEnumConverter]
public enum OrganizationSource
    : byte
{
    /// <summary>
    /// The Norwegian Central Coordinating Register for Legal Entities.
    /// </summary>
    [JsonStringEnumMemberName("ccr")]
    CentralCoordinatingRegister = 1,

    /// <summary>
    /// SDF - Businesses assessed as partnerships - Skatteetaten-registrerte selskaper (Selskap med deltakerfastsetting)
    /// </summary>
    [JsonStringEnumMemberName("sdf")]
    BusinessAssessedPartnerships,
}
