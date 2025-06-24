using Altinn.Authorization.ModelUtils;

namespace Altinn.Platform.Models.Register;

/// <summary>
/// Represents a self-identified user party in Altinn Register.
/// </summary>
[PolymorphicFieldValueRecord]
public sealed record SelfIdentifiedUser()
    : Party(PartyType.SelfIdentifiedUser)
{
}
