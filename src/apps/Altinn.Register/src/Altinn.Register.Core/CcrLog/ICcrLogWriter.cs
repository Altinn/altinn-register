using System.Buffers;
using System.Net;

namespace Altinn.Register.Core.CcrLog;

/// <summary>
/// Log writer for CCR SOAP requests.
/// </summary>
public interface ICcrLogWriter
{
    /// <summary>
    /// Log a CCR SOAP request and response.
    /// </summary>
    /// <param name="id">The request id.</param>
    /// <param name="requestStart">When the request started.</param>
    /// <param name="requestUrl">The request url.</param>
    /// <param name="requestHeaders">The request headers.</param>
    /// <param name="requestBody">The request body.</param>
    /// <param name="responseStatusCode">The response status code.</param>
    /// <param name="responseHeaders">The response headers.</param>
    /// <param name="responseBody">The response body.</param>
    /// <param name="duration">The request duration.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    public Task LogCcrSoapRequest(
        Guid id,
        DateTimeOffset requestStart,
        string requestUrl,
        ReadOnlySequence<byte> requestHeaders,
        ReadOnlySequence<byte> requestBody,
        HttpStatusCode responseStatusCode,
        ReadOnlySequence<byte> responseHeaders,
        ReadOnlySequence<byte> responseBody,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
