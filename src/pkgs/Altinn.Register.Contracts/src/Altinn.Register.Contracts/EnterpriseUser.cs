using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Contracts;

/// <summary>
/// Represents an enterprise user party in Altinn Register.
/// </summary>
[PolymorphicFieldValueRecord]
public sealed record EnterpriseUser()
    : Party(PartyType.EnterpriseUser)
{
    /// <summary>
    /// Gets the owner of the system user.
    /// </summary>
    [JsonPropertyName("owner")]
    public required FieldValue<PartyOwnerRef> Owner { get; init; }
}
