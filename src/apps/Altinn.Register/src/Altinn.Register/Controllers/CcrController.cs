using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Altinn.Authorization.ModelUtils.EnumUtils;
using Altinn.Register.Conventions;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.CcrLog;
using Altinn.Register.Core.Utils;
using Altinn.Register.Integrations.Ccr.Xml;
using Asp.Versioning;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nerdbank.Streams;

namespace Altinn.Register.Controllers;

/// <summary>
/// Proxy to SBL bridge for test environments.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("enhets-registeret/api/v{version:apiVersion}")]
public partial class CcrController
    : ControllerBase
{
    private static FlagsEnumModel<SkipRecordReasons> SkipRecordReasonModel { get; }
        = FlagsEnumModel.Create<SkipRecordReasons>();

    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CcrController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcrController"/> class.
    /// </summary>
    public CcrController(
        TimeProvider timeProvider,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CcrController> logger)
    {
        _timeProvider = timeProvider;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Updates the CCR data for a party.
    /// </summary>
    /// <remarks>
    /// Enhetsregisteret hurtig-oppdattering.
    /// </remarks>
    [HttpPost("update.svc")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ConfigurationCondition("Altinn:register:Ccr:Update:Enabled")]
    [RequestSizeLimit(50_000_000 /* 50 MB */)]
    public Task Update(
        [FromQuery(Name = "record")] bool record = true,
        CancellationToken cancellationToken = default)
    {
        if (Request.Headers.TryGetValue("X-Altinn-Register-Ccr", out var values)
            && values.Contains("Apply-In-A3", StringComparer.OrdinalIgnoreCase))
        {
            return HandleInA3(cancellationToken);
        }

        return ProxyToA2(record, cancellationToken);
    }

    private async Task ProxyToA2(bool record, CancellationToken cancellationToken)
    {
        using RecordOperationState state = new(_timeProvider.GetUtcNow(), Request.GetDisplayUrl());

        state.ReadRequestHeaders(Request.Headers);
        await state.ReadRequestBody(Request.BodyReader, cancellationToken);

        using var client = _httpClientFactory.CreateClient("a2:ccr");
        using var request = new HttpRequestMessage(HttpMethod.Post, "RegisterExternal/RegisterERExternalBasic.svc");
        state.WriteRequest(request);

        var start = _timeProvider.GetTimestamp();
        using var response = await client.SendAsync(request, cancellationToken);
        state.ResponseStatusCode = response.StatusCode;
        state.ReadResponseHeaders(response.Headers, response.Content.Headers);
        await state.ReadResponseBody(_logger, response.Content, cancellationToken);
        state.Duration = _timeProvider.GetElapsedTime(start);

        await state.WriteResponse(Response, cancellationToken);
        await Response.CompleteAsync();

        SkipRecordReasons skipReasons = SkipRecordReasons.None;
        if (!record)
        {
            skipReasons |= SkipRecordReasons.DisabledByQueryParameter;
        }

        if (!_configuration.GetValue("Altinn:register:Ccr:Update:Record", defaultValue: false))
        {
            skipReasons |= SkipRecordReasons.DisabledByConfiguration;
        }

        if (skipReasons == SkipRecordReasons.None)
        {
            Log.EnqueueRecordingCcrUpdate(_logger);
            EnqueueRecord(state);
        }
        else
        {
            Log.NotRecordingCcrUpdate(_logger, skipReasons);
        }
    }

    private async Task HandleInA3(CancellationToken cancellationToken)
    {
        try
        {
            var receivedAt = _timeProvider.GetUtcNow();
            await UpdateFromCcr(cancellationToken);

            //// TODO: Publish event

            await WriteAltinnSoapSuccess(receivedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.InteralUpdateError(_logger, ex);
            await WriteAltinnSoapFault("Client authentication failed", isClientFault: true);
        }
        catch (XmlException ex)
        {
            Log.InteralUpdateError(_logger, ex);
            await WriteAltinnSoapFault("Malformed XML in request", isClientFault: true);
        }
        catch (Exception ex)
        {
            Log.InteralUpdateError(_logger, ex);
            await WriteAltinnSoapFault("Internal server error", isClientFault: false);
        }
    }

    private async Task UpdateFromCcr(CancellationToken cancellationToken)
    {
        using var seq = new Sequence<byte>(ArrayPool<byte>.Shared);
        await Request.BodyReader.CopyToAsync(seq, cancellationToken);

        var result = CcrUpdateEnvelopeReader.ReadEnvelope(seq.AsReadOnlySequence);
        seq.Reset();

        {
            using var writer = new BufferTextWriter(seq, Encoding.UTF8);
            writer.Write(result.Payload);
            writer.Flush();
        }

        var ccrService = HttpContext.RequestServices.GetRequiredService<CcrService>();
        if (HttpContext.Connection.RemoteIpAddress is null
            || string.IsNullOrWhiteSpace(result.UserName)
            || string.IsNullOrWhiteSpace(result.Password)
            || !ccrService.AuthorizeCcrClient(result.UserName, result.Password, HttpContext.Connection.RemoteIpAddress))
        {
            ThrowHelper.ThrowUnauthorizedAccessException("Invalid CCR client credentials or missing source IP address");
        }

        await ccrService.UpdateFromCcr(
            commandId: Guid.CreateVersion7(),
            seq.AsReadOnlySequence,
            cancellationToken);
    }

    private async Task WriteAltinnSoapSuccess(DateTimeOffset timeReceived)
    {
        const string nsSoap = "http://schemas.xmlsoap.org/soap/envelope/";
        const string nsRegister = "http://www.altinn.no/services/Register/ER/2013/06";
        const string nsXsi = "http://www.w3.org/2001/XMLSchema-instance";

        Response.ContentType = "text/xml; charset=utf-8";

        var receiptXml = $"""
            <?xml version="1.0" encoding="UTF-8"?><ERReceipt schemaVersion="1.0" xmlns:xsi="{nsXsi}" xsi:noNamespaceSchemaLocation="GovAgencyReceipt.xsd"><DataUnitInReceipt receiptType="ER" status="OK_ER_DATA_PROCESSED" timeReceived="{timeReceived.UtcDateTime:yyyy-MM-ddTHH:mm:ss}"><Message><MessageEntry>ER data processed ok</MessageEntry></Message></DataUnitInReceipt></ERReceipt>
            """;

        await using var writer = XmlWriter.Create(Response.Body, new XmlWriterSettings { Async = true, CloseOutput = false });
        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(prefix: "s", localName: "Envelope", ns: nsSoap);
        await writer.WriteStartElementAsync(prefix: "s", localName: "Body", ns: nsSoap);
        await writer.WriteStartElementAsync(prefix: null, localName: "SubmitERDataBasicResponse", ns: nsRegister);
        await writer.WriteElementStringAsync(prefix: null, localName: "SubmitERDataBasicResult", ns: nsRegister, value: receiptXml);
        await writer.WriteEndElementAsync(); // SubmitERDataBasicResponse
        await writer.WriteEndElementAsync(); // Body
        await writer.WriteEndElementAsync(); // Envelope
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();
    }

    private async Task WriteAltinnSoapFault(string faultString, bool isClientFault)
    {
        const string nsSoap = "http://schemas.xmlsoap.org/soap/envelope/";
        const string nsAltinnFault = "http://www.altinn.no/services/common/fault/2009/10";
        const string nsXsi = "http://www.w3.org/2001/XMLSchema-instance";

        Response.StatusCode = (int)(isClientFault ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError);
        Response.ContentType = "text/xml; charset=utf-8";

        await using var writer = XmlWriter.Create(Response.Body, new XmlWriterSettings { Async = true, CloseOutput = false });
        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(prefix: "s", localName: "Envelope", ns: nsSoap);
        await writer.WriteStartElementAsync(prefix: "s", localName: "Body", ns: nsSoap);
        await writer.WriteStartElementAsync(prefix: "s", localName: "Fault", ns: nsSoap);

        await writer.WriteElementStringAsync(prefix: null, localName: "faultcode", ns: null, value: isClientFault ? "s:Client" : "s:Server");

        await writer.WriteStartElementAsync(prefix: null, localName: "faultstring", ns: null);
        await writer.WriteAttributeStringAsync(prefix: "xml", localName: "lang", ns: null, value: "nb-NO");
        await writer.WriteStringAsync("An error occurred");
        await writer.WriteEndElementAsync(); // faultstring

        await writer.WriteStartElementAsync(prefix: null, localName: "detail", ns: null);
        await writer.WriteStartElementAsync(prefix: null, localName: "AltinnFault", ns: nsAltinnFault);
        await writer.WriteAttributeStringAsync(prefix: "xmlns", localName: "i", ns: null, value: nsXsi);
        await writer.WriteElementStringAsync(prefix: null, localName: "AltinnErrorMessage", ns: nsAltinnFault, value: faultString);
        await writer.WriteElementStringAsync(prefix: null, localName: "AltinnExtendedErrorMessage", ns: nsAltinnFault, value: "No further information available");
        await writer.WriteElementStringAsync(prefix: null, localName: "AltinnLocalizedErrorMessage", ns: nsAltinnFault, value: "No further information available");
        await writer.WriteElementStringAsync(prefix: null, localName: "ErrorGuid", ns: nsAltinnFault, value: Guid.CreateVersion7().ToString());
        await writer.WriteElementStringAsync(prefix: null, localName: "ErrorID", ns: nsAltinnFault, value: "0");
        await writer.WriteElementStringAsync(prefix: null, localName: "UserGuid", ns: nsAltinnFault, value: "-no value-");
        await writer.WriteElementStringAsync(prefix: null, localName: "UserId", ns: nsAltinnFault, value: "0");
        await writer.WriteEndElementAsync(); // AltinnFault
        await writer.WriteEndElementAsync(); // detail

        await writer.WriteEndElementAsync(); // Fault
        await writer.WriteEndElementAsync(); // Body
        await writer.WriteEndElementAsync(); // Envelope
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();
    }

    private void EnqueueRecord(RecordOperationState state)
    {
        var clone = state.MoveOwnership();
        _ = Task.Run(async () =>
        {
            using var state = clone;
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var recorder = scope.ServiceProvider.GetRequiredService<ICcrLogWriter>();
            var cancellationToken = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<CcrController>>();

            try
            {
                Log.RecordingCcrUpdate(logger);
                await state.Record(recorder, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Application is shutting down - not an error
            }
            catch (Exception ex)
            {
                Log.ErrorRecordingCcrUpdate(logger, ex);
            }
        });
    }

    [Flags]
    private enum SkipRecordReasons
        : byte
    {
        [JsonStringEnumMemberName("none")]
        None = default,

        [JsonStringEnumMemberName("query-param")]
        DisabledByQueryParameter = 1 << 0,

        [JsonStringEnumMemberName("config")]
        DisabledByConfiguration = 1 << 1,
    }

    private sealed class RecordOperationState(DateTimeOffset now, string url)
        : IDisposable
    {
        private static readonly SearchValues<string> UnproxiedHeaders
            = SearchValues.Create(
                [
                    "connection",
                    "keep-alive",
                    "proxy-authenticate",
                    "proxy-authorization",
                    "te",
                    "trailer",
                    "transfer-encoding",
                    "upgrade",
                    "host", // re-calculated by HttpClient
                    "content-length", // re-calculated by HttpClient
                    "content-encoding", // handled by HttpClient
                    "x-forwarded-for",
                    "x-forwarded-host",
                    "x-forwarded-proto",
                ],
                StringComparison.OrdinalIgnoreCase);

        private readonly Guid _id = Guid.CreateVersion7();
        private readonly DateTimeOffset _requestStart = now;

        private readonly string _requestUrl = url;
        private Sequence<byte>? _requestHeaders;
        private Sequence<byte>? _requestBody;

        private HttpStatusCode _responseStatusCode;
        private Sequence<byte>? _responseHeaders;
        private Sequence<byte>? _responseBody;
        private TimeSpan _duration;

        private RecordOperationState(RecordOperationState source)
            : this(source._requestStart, source._requestUrl)
        {
            _id = source._id;

            _requestHeaders = source._requestHeaders;
            _requestBody = source._requestBody;

            _responseStatusCode = source._responseStatusCode;
            _responseHeaders = source._responseHeaders;
            _responseBody = source._responseBody;
            _duration = source._duration;
        }

        public RecordOperationState MoveOwnership()
        {
            RecordOperationState clone = new(this);

            // null out the fields in the source to prevent disposal
            _requestHeaders = null;
            _requestBody = null;
            _responseHeaders = null;
            _responseBody = null;

            return clone;
        }

        public Task Record(ICcrLogWriter recorder, CancellationToken cancellationToken)
        {
            return recorder.LogCcrSoapRequest(
                id: _id,
                requestStart: _requestStart,
                requestUrl: _requestUrl,
                requestHeaders: _requestHeaders,
                requestBody: _requestBody,
                responseStatusCode: _responseStatusCode,
                responseHeaders: _responseHeaders,
                responseBody: _responseBody,
                duration: _duration,
                cancellationToken: cancellationToken);
        }

        public HttpStatusCode ResponseStatusCode
        {
            set => _responseStatusCode = value;
        }

        public TimeSpan Duration
        {
            set => _duration = value;
        }

        public void ReadRequestHeaders(IHeaderDictionary headers)
        {
            Debug.Assert(_requestHeaders is null);
            _requestHeaders = new Sequence<byte>(ArrayPool<byte>.Shared);

            ReadHeaders(_requestHeaders, headers);
        }

        public void ReadResponseHeaders(HttpResponseHeaders responseHeaders, HttpContentHeaders contentHeaders)
        {
            Debug.Assert(_responseHeaders is null);
            _responseHeaders = new Sequence<byte>(ArrayPool<byte>.Shared);

            var headers = responseHeaders
                .Concat(contentHeaders)
                .Select(kvp => KeyValuePair.Create(kvp.Key, ToStringValues(kvp.Value)));

            ReadHeaders(_responseHeaders, headers);
        }

        private void ReadHeaders(IBufferWriter<byte> dest, IEnumerable<KeyValuePair<string, StringValues>> headers)
        {
            Debug.Assert(dest is not null);
            Debug.Assert(headers is not null);

            using var writer = new Utf8JsonWriter(dest);
            writer.WriteStartObject();
            foreach (var (name, values) in headers)
            {
                if (values.Count == 0)
                {
                    continue;
                }

                writer.WritePropertyName(name);
                if (values.Count == 1)
                {
                    writer.WriteStringValue(values[0]);
                }
                else
                {
                    writer.WriteStartArray();
                    foreach (var value in values)
                    {
                        writer.WriteStringValue(value);
                    }

                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
        }

        public async ValueTask ReadRequestBody(PipeReader reader, CancellationToken cancellationToken)
        {
            Debug.Assert(_requestBody is null);
            _requestBody = new Sequence<byte>(ArrayPool<byte>.Shared);

            ReadResult result;
            do
            {
                result = await reader.ReadAsync(cancellationToken);
                _requestBody.Write(result.Buffer);
                reader.AdvanceTo(result.Buffer.End);
            }
            while (!result.IsCompleted);
        }

        public async ValueTask ReadResponseBody(ILogger logger, HttpContent content, CancellationToken cancellationToken)
        {
            Debug.Assert(_responseBody is null);
            _responseBody = new Sequence<byte>(ArrayPool<byte>.Shared);

            using var stream = await content.ReadAsStreamAsync(cancellationToken);

            ReadOnlyMemory<string> encodings = content.Headers.ContentEncoding.ToArray();
            await CopyResponse(logger, stream, _responseBody, encodings, cancellationToken);

            static async Task CopyResponse(ILogger logger, Stream source, Sequence<byte> destination, ReadOnlyMemory<string> encodings, CancellationToken cancellationToken)
            {
                if (encodings.IsEmpty)
                {
                    await CopyToSequenceAsync(source, destination, cancellationToken);
                    return;
                }

                var encoding = encodings.Span[^1];
                var remainingEncodings = encodings[..^1];

                if (string.Equals(encoding, "gzip", StringComparison.OrdinalIgnoreCase))
                {
                    using var gzipStream = new GZipStream(source, CompressionMode.Decompress);
                    await CopyResponse(logger, gzipStream, destination, remainingEncodings, cancellationToken);
                }
                else if (string.Equals(encoding, "deflate", StringComparison.OrdinalIgnoreCase))
                {
                    using var deflateStream = new DeflateStream(source, CompressionMode.Decompress);
                    await CopyResponse(logger, deflateStream, destination, remainingEncodings, cancellationToken);
                }
                else if (string.Equals(encoding, "br", StringComparison.OrdinalIgnoreCase))
                {
                    using var brotliStream = new BrotliStream(source, CompressionMode.Decompress);
                    await CopyResponse(logger, brotliStream, destination, remainingEncodings, cancellationToken);
                }
                else
                {
                    // Unsupported encoding - log and copy as is
                    Log.UnsupportedContentEncoding(logger, encoding);
                    await CopyToSequenceAsync(source, destination, cancellationToken);
                }
            }

            static async Task CopyToSequenceAsync(Stream source, Sequence<byte> destination, CancellationToken cancellationToken)
            {
                int read;
                do
                {
                    Memory<byte> buffer = destination.GetMemory(1024 * 32 /* 32 KiB */);
                    read = await source.ReadAsync(buffer, cancellationToken);
                    destination.Advance(read);
                }
                while (read != 0);
            }
        }

        public ValueTask WriteResponse(HttpResponse response, CancellationToken cancellationToken)
        {
            Debug.Assert(response is not null);
            Debug.Assert(_responseHeaders is not null);
            Debug.Assert(_responseBody is not null);

            response.StatusCode = (int)_responseStatusCode;
            WriteResponseHeaders(response, _responseHeaders);
            return WriteResponseBody(response.BodyWriter, _responseBody, cancellationToken);
        }

        public void WriteRequest(HttpRequestMessage request)
        {
            Debug.Assert(request is not null);
            Debug.Assert(_requestHeaders is not null);
            Debug.Assert(_requestBody is not null);

            request.Content = new StreamContent(_requestBody.AsReadOnlySequence.AsStream());
            WriteRequestHeaders(request, _requestHeaders);
        }

        public void Dispose()
        {
            _requestBody?.Dispose();
            _responseBody?.Dispose();
            _responseHeaders?.Dispose();
            _requestHeaders?.Dispose();
        }

        private static void WriteResponseHeaders(HttpResponse response, Sequence<byte> headers)
        {
            Debug.Assert(response is not null);
            Debug.Assert(headers is not null);

            WriteHeaders(headers, response, static (response, name, values) =>
            {
                if (ShouldIgnoreHeader(name))
                {
                    return;
                }

                response.Headers.Append(name, values);
            });

            static bool ShouldIgnoreHeader(string name)
                => UnproxiedHeaders.Contains(name);
        }

        private static void WriteRequestHeaders(HttpRequestMessage request, Sequence<byte> headers)
        {
            Debug.Assert(request is not null);
            Debug.Assert(headers is not null);

            WriteHeaders(headers, request, static (request, name, values) =>
            {
                if (ShouldIgnoreHeader(name))
                {
                    return;
                }

                if (!request.Headers.TryAddWithoutValidation(name, (IEnumerable<string>)values))
                {
                    request.Content?.Headers.TryAddWithoutValidation(name, (IEnumerable<string>)values);
                }
            });

            static bool ShouldIgnoreHeader(string name)
                => UnproxiedHeaders.Contains(name);
        }

        private static void WriteHeaders<T>(Sequence<byte> headers, T target, Action<T, string, StringValues> writeHeader)
        {
            Debug.Assert(target is not null);
            Debug.Assert(headers is not null);

            Utf8JsonReader reader = new(headers.AsReadOnlySequence);
            reader.Read(); // find first token
            Debug.Assert(reader.TokenType == JsonTokenType.StartObject);
            reader.Read(); // StartObject

            while (reader.TokenType != JsonTokenType.EndObject)
            {
                Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
                var name = reader.GetString()!;

                reader.Read();
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString()!;
                    writeHeader(target, name, value);
                    reader.Read();
                }
                else
                {
                    Debug.Assert(reader.TokenType == JsonTokenType.StartArray);
                    reader.Read();

                    var values = new List<string>();
                    while (reader.TokenType != JsonTokenType.EndArray)
                    {
                        values.Add(reader.GetString()!);
                        reader.Read();
                    }

                    writeHeader(target, name, values.ToArray());
                    reader.Read();
                }
            }
        }

        private static async ValueTask WriteResponseBody(PipeWriter writer, ReadOnlySequence<byte> data, CancellationToken cancellationToken)
        {
            if (data.IsEmpty)
            {
                await writer.CompleteAsync();
                return;
            }

            if (data.IsSingleSegment)
            {
                var segment = data.First;
                await writer.WriteAsync(segment, cancellationToken);
                await writer.CompleteAsync();
                return;
            }

            foreach (var segment in data)
            {
                await writer.WriteAsync(segment, cancellationToken);
            }

            await writer.CompleteAsync();
        }

        private static StringValues ToStringValues(IEnumerable<string> source)
        {
            if (source is StringValues sv)
            {
                return sv;
            }
            else if (source is string[] arr)
            {
                return arr;
            }

            if (source.TryGetNonEnumeratedCount(out var count))
            {
                if (count == 0)
                {
                    return StringValues.Empty;
                }

                if (count == 1)
                {
                    return new(source.First());
                }
            }

            return new([.. source]);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Information, "Enqueueing recording of CCR update")]
        public static partial void EnqueueRecordingCcrUpdate(ILogger logger);

        [LoggerMessage(1, LogLevel.Information, "Recording CCR update")]
        public static partial void RecordingCcrUpdate(ILogger logger);

        [LoggerMessage(2, LogLevel.Error, "Error recording CCR update")]
        public static partial void ErrorRecordingCcrUpdate(ILogger logger, Exception ex);

        [LoggerMessage(3, LogLevel.Information, "Not recording CCR update due to {Reasons}")]
        private static partial void NotRecordingCcrUpdate(ILogger logger, string reasons);

        public static void NotRecordingCcrUpdate(ILogger logger, SkipRecordReasons reasons)
            => NotRecordingCcrUpdate(logger, SkipRecordReasonModel.Format(reasons));

        [LoggerMessage(4, LogLevel.Information, "Unsupported content-encoding: {Encoding}")]
        public static partial void UnsupportedContentEncoding(ILogger logger, string encoding);

        [LoggerMessage(5, LogLevel.Error, "Error during internal update from CCR")]
        public static partial void InteralUpdateError(ILogger logger, Exception ex);
    }
}
