using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Contracts;

/// <summary>
/// Represents a system user party in Altinn Register.
/// </summary>
[PolymorphicFieldValueRecord]
public sealed record SystemUser()
    : Party(PartyType.SystemUser)
{
}
