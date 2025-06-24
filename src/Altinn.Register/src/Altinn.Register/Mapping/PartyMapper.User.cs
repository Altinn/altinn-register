#nullable enable

using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Models.Register;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Mapping;

/// <summary>
/// Mappers for converting from <see cref="PartyRecord"/>
/// to <see cref="Party"/>.
/// </summary>
internal static partial class PartyMapper
{
    [return: NotNullIfNotNull(nameof(source))]
    private static PartyUser? MapPartyUser(PartyUserRecord? source)
    {
        if (source is null)
        {
            return null;
        }

        return new PartyUser(
            userId: source.UserId,
            username: source.Username,
            userIds: source.UserIds);
    }
}
