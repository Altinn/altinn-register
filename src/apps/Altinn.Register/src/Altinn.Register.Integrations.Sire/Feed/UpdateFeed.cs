using System.Text.Json.Serialization;

namespace Altinn.Register.Integrations.Sire.Feed;

/// <summary>
/// The outer envelope of a SIRE event-feed page.
/// </summary>
public sealed record UpdateFeed
{
    /// <summary>
    /// Gets the events in this page, ordered by <see cref="UpdateItem.SequenceNumber"/>.
    /// </summary>
    [JsonPropertyName("hendelser")]
    public IReadOnlyList<UpdateItem>? Updates { get; init; }
}
