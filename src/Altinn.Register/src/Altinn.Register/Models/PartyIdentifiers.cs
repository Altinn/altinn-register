#nullable enable

using System;
using System.Text.Json.Serialization;
using Altinn.Platform.Models.Register.V1;

namespace Altinn.Register.Models;

/// <summary>
/// A set of identifiers for a party.
/// </summary>
public record PartyIdentifiers
{
    /// <summary>
    /// The party id.
    /// </summary>
    public required int PartyId { get; init; }

    /// <summary>
    /// The party uuid.
    /// </summary>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// The organization number of the party (if applicable).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrgNumber { get; init; }

    /// <summary>
    /// The social security number of the party (if applicable and included).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SSN { get; init; }

    /// <summary>
    /// Create a new <see cref="PartyIdentifiers"/> from a <see cref="Party"/>.
    /// </summary>
    /// <param name="party">The <see cref="Party"/> from which to create the <see cref="PartyIdentifiers"/>.</param>
    /// <param name="includeSSN">Whether or not to include SSN (if any).</param>
    /// <returns>A <see cref="PartyIdentifiers"/>.</returns>
    public static PartyIdentifiers Create(Party party, bool includeSSN = false)
    {
        var id = party.PartyId;
        var uuid = party.PartyUuid!.Value;
        var orgNo = NullIfEmpty(party.OrgNumber);
        var ssn = includeSSN ? NullIfEmpty(party.SSN) : null;

        return new PartyIdentifiers
        {
            PartyId = id,
            PartyUuid = uuid,
            OrgNumber = orgNo,
            SSN = ssn,
        };

        static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
