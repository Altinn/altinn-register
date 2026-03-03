#nullable enable

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text.Json;
using Asp.Versioning;
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
[Route("register/api/v{version:apiVersion}/ccr")]
public class CcrController
    : ControllerBase
{
    private readonly TimeProvider _timeProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcrController"/> class.
    /// </summary>
    public CcrController(
        TimeProvider timeProvider,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory serviceScopeFactory)
    {
        _timeProvider = timeProvider;
        _httpClientFactory = httpClientFactory;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Updates the CCR data for a party.
    /// </summary>
    /// <remarks>
    /// Enhetsregisteret hurtig-oppdattering.
    /// </remarks>
    [HttpPost("update")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task Update(CancellationToken cancellationToken = default)
    {
        using RecordOperationState state = new(_timeProvider.GetUtcNow(), Request.GetDisplayUrl());

        state.ReadRequestHeaders(Request.Headers);
        await state.ReadRequestBody(Request.BodyReader, cancellationToken);

        using var client = _httpClientFactory.CreateClient("a2:ccr");
        using var request = new HttpRequestMessage(HttpMethod.Post, "RegisterExternal/RegisterERExternalBasic.svc?wsdl");
        state.WriteRequest(request);

        using var response = await client.SendAsync(request, cancellationToken);
        state.ReadResponseHeaders(response.Headers, response.Content.Headers);
        await state.ReadResponseBody(response.Content, cancellationToken);

        await state.WriteResponse(Response, cancellationToken);
        await Response.CompleteAsync();

        // TODO: Save state to database
    }

    private sealed class RecordOperationState(DateTimeOffset now, string url)
        : IDisposable
    {
        private readonly Guid _id = Guid.CreateVersion7();
        private readonly DateTimeOffset _requestStart = now;

        private readonly string _requestUrl = url;
        private Sequence<byte>? _requestHeaders;
        private Sequence<byte>? _requestBody;

        private Sequence<byte>? _responseHeaders;
        private Sequence<byte>? _responseBody;

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

        public async ValueTask ReadResponseBody(HttpContent content, CancellationToken cancellationToken)
        {
            Debug.Assert(_responseBody is null);
            _responseBody = new Sequence<byte>(ArrayPool<byte>.Shared);

            using var stream = await content.ReadAsStreamAsync(cancellationToken);
            int read;
            do
            {
                Memory<byte> buffer = _responseBody.GetMemory(1024 * 32 /* 32 KiB */);
                read = await stream.ReadBlockAsync(buffer, cancellationToken);
                _responseBody.Advance(read);
            }
            while (read != 0);
        }

        public ValueTask WriteResponse(HttpResponse response, CancellationToken cancellationToken)
        {
            Debug.Assert(response is not null);
            Debug.Assert(_responseHeaders is not null);
            Debug.Assert(_responseBody is not null);

            WriteResponseHeaders(response, _responseHeaders);
            return WriteResponseBody(response.BodyWriter, _responseBody, cancellationToken);
        }

        public void WriteRequest(HttpRequestMessage request)
        {
            Debug.Assert(request is not null);
            Debug.Assert(_requestHeaders is not null);
            Debug.Assert(_responseBody is not null);

            WriteRequestHeaders(request, _requestHeaders);
            throw new NotImplementedException();
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

            WriteHeaders(headers, response, static (response, name, values) => response.Headers.Append(name, values));
        }

        private static void WriteRequestHeaders(HttpRequestMessage request, Sequence<byte> headers)
        {
            Debug.Assert(request is not null);
            Debug.Assert(headers is not null);

            WriteHeaders(headers, request, static (request, name, values) =>
            {
                if (!request.Headers.TryAddWithoutValidation(name, (IEnumerable<string>)values))
                {
                    request.Content?.Headers.TryAddWithoutValidation(name, (IEnumerable<string>)values);
                }
            });
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
}
