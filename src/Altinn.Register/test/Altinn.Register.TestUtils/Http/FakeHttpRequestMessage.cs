using System.Buffers;
using Nerdbank.Streams;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// A fake http request message.
/// </summary>
public class FakeHttpRequestMessage
    : HttpRequestMessage
{
    internal static async Task<FakeHttpRequestMessage> Create(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Sequence<byte>? buffer = null;
        SequenceHttpContent? sequenceContent = null;
        try
        {
            if (request.Content is { } content)
            {
                buffer = new(ArrayPool<byte>.Shared);
                await content.CopyToAsync(buffer.AsStream(), cancellationToken);
                sequenceContent = new SequenceHttpContent(buffer);
                buffer = null;

                foreach (var h in content.Headers)
                {
                    sequenceContent.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }

            FakeHttpRequestMessage ret = new(request, sequenceContent);
            sequenceContent = null;
            return ret;
        }
        finally
        {
            sequenceContent?.Dispose();
            buffer?.Dispose();
        }
    }

    private FakeHttpRequestMessage(HttpRequestMessage original, SequenceHttpContent? content)
        : base(original.Method, original.RequestUri)
    {
        base.Content = content;
        Version = original.Version;
        VersionPolicy = original.VersionPolicy;
        
        foreach (var h in original.Headers)
        {
            Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        foreach (var o in original.Options)
        {
            ((IDictionary<string, object?>)Options)[o.Key] = o.Value;
        }
    }

    /// <inheritdoc cref="HttpRequestMessage.Content"/>
    public new SequenceHttpContent? Content
    {
        get => (SequenceHttpContent?)base.Content;
    }
}
