using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// A database record for a self-identified user.
/// </summary>
[PolymorphicFieldValueRecord]
public sealed record SelfIdentifiedUserRecord
    : PartyRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelfIdentifiedUserRecord"/> class.
    /// </summary>
    public SelfIdentifiedUserRecord()
        : base(Parties.PartyType.SelfIdentifiedUser)
    {
    }
}
