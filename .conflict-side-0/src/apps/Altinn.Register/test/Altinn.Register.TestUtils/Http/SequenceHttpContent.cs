using System.Buffers;
using System.Net;
using Nerdbank.Streams;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// Represents a <see cref="HttpContent"/> backed by a <see cref="ReadOnlySequence{T}"/> of bytes.
/// </summary>
public class SequenceHttpContent
    : HttpContent
{
    private readonly Sequence<byte> _buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequenceHttpContent"/> class.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    public SequenceHttpContent(Sequence<byte> buffer)
    {
        _buffer = buffer;
    }

    /// <inheritdoc/>
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => stream.WriteAsync(_buffer.AsReadOnlySequence).AsTask();

    /// <inheritdoc/>
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        => stream.WriteAsync(_buffer.AsReadOnlySequence, cancellationToken).AsTask();

    /// <inheritdoc/>
    protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
        => _buffer.AsReadOnlySequence.AsStream();

    /// <inheritdoc/>
    protected override Task<Stream> CreateContentReadStreamAsync()
        => Task.FromResult(_buffer.AsReadOnlySequence.AsStream());

    /// <inheritdoc/>
    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        => Task.FromResult(_buffer.AsReadOnlySequence.AsStream());

    /// <inheritdoc/>
    protected override bool TryComputeLength(out long length)
    {
        length = _buffer.Length;
        return true;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }
}
