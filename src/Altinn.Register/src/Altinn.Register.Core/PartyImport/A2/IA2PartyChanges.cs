namespace Altinn.Register.Core.PartyImport.A2;

/// <summary>
/// A <see cref="IAsyncEnumerable{T}"/> of <see cref="A2PartyChange"/> which includes the ability to
/// read the last change id.
/// </summary>
public interface IA2PartyChanges
    : IAsyncEnumerable<A2PartyChange>
{
    /// <summary>
    /// Gets the last change id.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The last change id.</returns>
    ValueTask<uint> GetLastChangeId(CancellationToken cancellationToken = default);
}
