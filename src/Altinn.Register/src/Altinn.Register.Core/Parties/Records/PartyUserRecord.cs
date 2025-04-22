using Altinn.Authorization.ModelUtils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// Represents a record for user-info of a party, with optional historical records.
/// </summary>
[FieldValueRecord]
public sealed record PartyUserRecord
{
    private readonly FieldValue<ImmutableValueArray<uint>> _userIds;

    /// <summary>
    /// Gets the user id.
    /// </summary>
    public FieldValue<uint> UserId
        => UserIds.Select(static ids => ids[0]);

    /// <summary>
    /// Gets the (historical) user ids of the party.
    /// </summary>
    /// <remarks>
    /// Should never be empty, but can be null or unset. The first user id is the current one - the rest should be ordered in descending order.
    /// </remarks>
    public required FieldValue<ImmutableValueArray<uint>> UserIds 
    {
        get => _userIds;
        init
        {
            if (value.HasValue)
            {
                Guard.IsNotEmpty(value.Value.AsSpan(), nameof(value));
            }

            _userIds = value;
        }
    }
}
