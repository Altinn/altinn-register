using System.Buffers;
using System.Collections.Immutable;
using System.Xml;
using Nerdbank.Streams;

namespace Altinn.Register.Integrations.Ccr.Xml;

public sealed class CcrXmlProcessor
{
    public static async IAsyncEnumerable<CcrPartyUpdate> ProcessCcrXmlAsync(ReadOnlySequence<byte> xmlData, CancellationToken cancellationToken = default)
    {
        using var reader = XmlReader.Create(xmlData.AsStream());

        // 1. read outer (root) node
        // 2. read header
        // 3. while (read enhet node)
        //   3.1. read enhet node into a CcrPartyUpdate
        //   3.2. yield return the CcrPartyUpdate
        // 4. read footer
        yield break;
    }
}

// TODO: move to separate files etc.
public abstract class CcrPartyUpdate
{
}

public sealed class CcrFullUpdate
    : CcrPartyUpdate
{
    public CcrFullUpdate()
    {
        throw new NotImplementedException();
    }
}

public sealed class CcrDeltaUpdate
    : CcrPartyUpdate
{
    private readonly ImmutableArray<CcrPartyDeltaOperation> _operations;

    public CcrDeltaUpdate(ImmutableArray<CcrPartyDeltaOperation> operations)
    {
        _operations = operations;
    }
}

public abstract class CcrPartyDeltaOperation
{
    // TODO: define delta operations, e.g. UpdateEmail, UpdateName, etc.
    public sealed class UpdateEmail
        : CcrPartyDeltaOperation
    {
        public string NewEmail { get; init; }
    }
}
