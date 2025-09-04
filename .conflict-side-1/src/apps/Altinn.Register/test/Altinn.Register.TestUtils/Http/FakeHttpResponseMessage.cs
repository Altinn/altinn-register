using System.Buffers;
using Nerdbank.Streams;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// A fake http response message.
/// </summary>
public class FakeHttpResponseMessage
    : HttpResponseMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FakeHttpResponseMessage"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    public FakeHttpResponseMessage(FakeHttpRequestMessage request)
        : base()
    {
        base.RequestMessage = request;
    }

    /// <inheritdoc cref="HttpResponseMessage.RequestMessage"/>
    public new FakeHttpRequestMessage RequestMessage
    {
        get => (FakeHttpRequestMessage)base.RequestMessage!;
    }

    /// <inheritdoc cref="HttpResponseMessage.Content"/>
    public new SequenceHttpContent? Content
    {
        get => (SequenceHttpContent)base.Content;
    }

    /// <summary>
    /// Sets the content of the response.
    /// </summary>
    /// <param name="content">The <see cref="HttpContent"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    public async Task SetContent(HttpContent content, CancellationToken cancellationToken)
    {
        if (content is SequenceHttpContent s)
        {
            base.Content = s;
            return;
        }

        Sequence<byte>? buffer = null;
        SequenceHttpContent? sequenceContent = null;
        try
        {
            buffer = new(ArrayPool<byte>.Shared);
            await content.CopyToAsync(buffer.AsStream(), cancellationToken);
            sequenceContent = new SequenceHttpContent(buffer);
            buffer = null;

            foreach (var h in content.Headers)
            {
                sequenceContent.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            base.Content = sequenceContent;
            sequenceContent = null;
        }
        finally
        {
            sequenceContent?.Dispose();
            buffer?.Dispose();
        }
    }
}
