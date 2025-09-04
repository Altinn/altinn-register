using System.Buffers;

namespace Altinn.Register.TestUtils.Http;

internal static class StreamExtensions
{
    public static ValueTask WriteAsync(this Stream stream, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsSingleSegment)
        {
            return stream.WriteAsync(buffer.First, cancellationToken);
        }

        return WriteMultiSegmentAsync(stream, buffer, cancellationToken);
    }

    private static async ValueTask WriteMultiSegmentAsync(Stream stream, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
    {
        var position = buffer.Start;
        while (buffer.TryGet(ref position, out var segment))
        {
            await stream.WriteAsync(segment, cancellationToken);
        }
    }
}
