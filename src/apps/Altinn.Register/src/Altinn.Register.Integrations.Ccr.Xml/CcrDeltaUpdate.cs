using System.Collections.Immutable;

namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Represents a set of delta operations to be applied to a Norwegian Central Coordinating Register for Legal Entities (CCR) (Enhetsregisteret ER) party as an update.
/// </summary>
/// <remarks>This class encapsulates a collection of changes (delta operations) that modify the state of a CCR
/// party. It is typically used to efficiently synchronize only the differences between party states, rather than
/// transmitting the entire state. Instances of this class are immutable and thread-safe.</remarks>
public sealed class CcrDeltaUpdate
    : CcrPartyUpdate
{
    private readonly ImmutableArray<CcrPartyDeltaOperation> _operations;

    /// <summary>
    /// Initializes a new instance of the CcrDeltaUpdate class with the specified collection of party delta operations.
    /// </summary>
    /// <param name="operations">The collection of party delta operations to include in this update. The array must not be empty.</param>
    public CcrDeltaUpdate(ImmutableArray<CcrPartyDeltaOperation> operations)
    {
        _operations = operations;
    }
}
