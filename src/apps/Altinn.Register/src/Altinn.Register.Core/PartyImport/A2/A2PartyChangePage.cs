using System.Collections;
using System.Collections.Immutable;

namespace Altinn.Register.Core.PartyImport.A2;

/// <summary>
/// A page of <see cref="A2PartyChange"/>s.
/// </summary>
public sealed class A2PartyChangePage(ImmutableArray<A2PartyChange> parties, uint lastKnownChangeId)
    : IReadOnlyList<A2PartyChange>
{
    /// <inheritdoc/>
    public A2PartyChange this[int index]
        => parties[index];

    /// <summary>
    /// Gets the <see cref="A2PartyChange.ChangeId"/> of the last known change at the time this <see cref="A2PartyChangePage"/> was fetched.
    /// </summary>
    public uint LastKnownChangeId
        => lastKnownChangeId;

    /// <inheritdoc/>
    public int Count
        => parties.Length;

    /// <summary>
    /// Returns an enumerator that iterates through the page.
    /// </summary>
    /// <returns>An enumerator.</returns>
    public ImmutableArray<A2PartyChange>.Enumerator GetEnumerator()
        => parties.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<A2PartyChange> IEnumerable<A2PartyChange>.GetEnumerator()
        => ((IEnumerable<A2PartyChange>)parties).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable)parties).GetEnumerator();
}
