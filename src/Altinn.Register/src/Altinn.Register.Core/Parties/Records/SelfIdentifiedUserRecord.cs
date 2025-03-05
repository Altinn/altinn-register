using System.Text.Json.Serialization;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// A database record for a self-identified user.
/// </summary>
[JsonConverter(typeof(PartyRecordJsonConverter))]
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
