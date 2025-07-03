using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Contracts;

/// <summary>
/// Represents an enterprise user party in Altinn Register.
/// </summary>
[PolymorphicFieldValueRecord]
public sealed record EnterpriseUser()
    : Party(PartyType.EnterpriseUser)
{
}
