using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Npr;

/// <summary>
/// Represents a page of NPR updates. The updates are ordered by their sequence number.
/// </summary>
public sealed record NprUpdatePage
    : IReadOnlyList<NprUpdate>
{
    private readonly ImmutableValueArray<NprUpdate> _updates;
    private uint _seqMax;

    /// <summary>
    /// Initializes a new instance of the <see cref="NprUpdatePage"/> class with the specified updates.
    /// </summary>
    /// <param name="updates">The updates in the page. Must not be empty.</param>
    public NprUpdatePage(ImmutableValueArray<NprUpdate> updates)
    {
        Guard.IsNotEmpty((IReadOnlyCollection<NprUpdate>)updates);
        Debug.Assert(IsSorted(updates));

        _updates = updates;
        _seqMax = updates[^1].SequenceNumber;
    }

    /// <inheritdoc/>
    public NprUpdate this[int index]
        => _updates[index];

    /// <inheritdoc/>
    public int Count
        => _updates.Length;

    /// <summary>
    /// Gets the highest sequence number among the updates in this page.
    /// </summary>
    public uint SeqMax
        => _seqMax;

    private static bool IsSorted(ImmutableValueArray<NprUpdate> updates)
    {
        var prev = uint.MinValue;
        foreach (var update in updates)
        {
            if (update.SequenceNumber <= prev)
            {
                return false;
            }

            prev = update.SequenceNumber;
        }

        return true;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public ImmutableArray<NprUpdate>.Enumerator GetEnumerator()
        => _updates.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<NprUpdate> IEnumerable<NprUpdate>.GetEnumerator()
        => ((IEnumerable<NprUpdate>)_updates).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable)_updates).GetEnumerator();
}
