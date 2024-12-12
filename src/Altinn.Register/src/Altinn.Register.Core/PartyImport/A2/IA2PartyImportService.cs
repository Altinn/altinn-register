using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;

namespace Altinn.Register.Core.PartyImport.A2;

/// <summary>
/// Service for importing parties from Altinn 2.
/// </summary>
public interface IA2PartyImportService
{
    /// <summary>
    /// Gets the changes that have occurred since the given change id.
    /// </summary>
    /// <param name="fromExclusive">The previously imported change id. Defaults to <c>0</c>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="A2PartyChangePage"/>s.</returns>
    IAsyncEnumerable<A2PartyChangePage> GetChanges(uint fromExclusive = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a party by its UUID.
    /// </summary>
    /// <param name="partyUuid">The party UUID.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="Party"/>.</returns>
    Task<PartyRecord> GetParty(Guid partyUuid, CancellationToken cancellationToken = default);
}
