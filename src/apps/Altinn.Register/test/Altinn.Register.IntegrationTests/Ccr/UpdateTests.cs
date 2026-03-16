using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Authorization.TestUtils.Http;
using Altinn.Register.TestsUtils.Npgsql;
using Microsoft.Extensions.Primitives;
using Npgsql;

namespace Altinn.Register.IntegrationTests.Ccr;

public class UpdateTests
    : IntegrationTestBase
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new StringValuesJsonConverter(),
        },
    };

    [Fact]
    public async Task Update_Proxies_Request_Headers_And_Body()
    {
        const string requestBody = "<soapenv:Envelope><soapenv:Body><request>payload</request></soapenv:Body></soapenv:Envelope>";

        FakeHttpHandlers.For("a2:ccr")
            .Expect(HttpMethod.Post, "RegisterExternal/RegisterERExternalBasic.svc")
            .Respond(async (FakeHttpRequestMessage request) =>
            {
                TimeProvider.Advance(TimeSpan.FromSeconds(0.2));

                request.Headers.TryGetValues("X-Test-Header", out var headerValues).ShouldBeTrue();
                headerValues.ShouldContain("test-value");

                request.Content.ShouldNotBeNull();
                var forwardedBody = await request.Content.ReadAsStringAsync(CancellationToken);
                forwardedBody.ShouldBe(requestBody);

                return HttpStatusCode.OK;
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, "enhets-registeret/api/v1/update.svc")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "text/xml"),
        };
        request.Headers.Add("X-Test-Header", "test-value");

        using var response = await HttpClient.SendAsync(request, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var row = await WaitForSingleLogRow();
        row.RequestUrl.ShouldEndWith("/enhets-registeret/api/v1/update.svc");
        row.RequestHeaders.ShouldContainKeyAndValue("X-Test-Header", "test-value");
        row.RequestBody.ShouldBe(requestBody);
        row.ResponseStatusCode.ShouldBe(HttpStatusCode.OK);
        row.ResponseBody.ShouldBe(string.Empty);
        row.Duration.ShouldBe(TimeSpan.FromSeconds(0.2));
    }

    [Fact]
    public async Task Update_Proxies_Response_Headers_And_Body()
    {
        const string requestBody = "<soapenv:Envelope><soapenv:Body><request>payload</request></soapenv:Body></soapenv:Envelope>";
        const string responseBody = "<soapenv:Envelope><soapenv:Body><result>ok</result></soapenv:Body></soapenv:Envelope>";

        FakeHttpHandlers.For("a2:ccr")
            .Expect(HttpMethod.Post, "RegisterExternal/RegisterERExternalBasic.svc")
            .Respond(() =>
            {
                TimeProvider.Advance(TimeSpan.FromSeconds(0.5));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Headers =
                    {
                        { "X-Upstream-Header", "upstream-value" },
                    },
                    Content = CreateMultiEncodedContent(responseBody),
                };
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, "enhets-registeret/api/v1/update.svc")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "text/xml"),
        };

        using var response = await HttpClient.SendAsync(request, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Upstream-Header", out var headerValues).ShouldBeTrue();
        headerValues.ShouldContain("upstream-value");

        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/xml");
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        body.ShouldBe(responseBody);

        var row = await WaitForSingleLogRow();
        row.RequestUrl.ShouldEndWith("/enhets-registeret/api/v1/update.svc");
        row.RequestBody.ShouldBe(requestBody);
        row.ResponseStatusCode.ShouldBe(HttpStatusCode.OK);
        row.ResponseHeaders.ShouldContainKeyAndValue("X-Upstream-Header", "upstream-value");
        row.ResponseBody.ShouldBe(responseBody);
        row.Duration.ShouldBe(TimeSpan.FromSeconds(0.5));

        static HttpContent CreateMultiEncodedContent(string text)
        {
            string[] encodings = ["gzip", "deflate", "br", "gzip", "deflate", "br", "gzip", "deflate", "br", "gzip"];
            var input = Encoding.UTF8.GetBytes(text);

            foreach (var encoding in encodings)
            {
                using var ms = new MemoryStream();
                Stream compressor = encoding switch
                {
                    "gzip" => new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true),
                    "deflate" => new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true),
                    "br" => new BrotliStream(ms, CompressionLevel.Optimal, leaveOpen: true),
                    _ => throw new InvalidOperationException($"Unsupported test encoding: {encoding}"),
                };

                using (compressor)
                {
                    compressor.Write(input, 0, input.Length);
                }

                input = ms.ToArray();
            }

            var content = new ByteArrayContent(input);
            content.Headers.ContentType = new("text/xml", "utf-8");
            foreach (var encoding in encodings)
            {
                content.Headers.ContentEncoding.Add(encoding);
            }

            return content;
        }
    }

    private async Task<CcrSoapLogRow> WaitForSingleLogRow()
    {
        var db = GetRequiredService<NpgsqlDataSource>();

        for (var i = 0; i < 50; i++)
        {
            await using var conn = await db.OpenConnectionAsync(CancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT request_url, request_headers::text, request_body, response_http_status, response_headers::text, response_body, duration
                FROM register.ccr_soap_log
                ORDER BY request_start DESC
                LIMIT 1
                """;

            await using var reader = await cmd.ExecuteReaderAsync(CancellationToken);
            if (await reader.ReadAsync(CancellationToken))
            {
                return new CcrSoapLogRow(
                    RequestUrl: reader.GetFieldValue<string>(0),
                    RequestHeaders: reader.GetJsonFieldValue<Dictionary<string, StringValues>>(1, Options)!,
                    RequestBody: reader.GetFieldValue<string>(2),
                    ResponseStatusCode: (HttpStatusCode)reader.GetFieldValue<int>(3),
                    ResponseHeaders: reader.GetJsonFieldValue<Dictionary<string, StringValues>>(4, Options)!,
                    ResponseBody: reader.GetFieldValue<string>(5),
                    Duration: reader.GetFieldValue<TimeSpan>(6));
            }

            await Task.Delay(100, CancellationToken);
        }

        throw new ShouldAssertException("Expected a row in register.ccr_soap_log, but none was recorded.");
    }

    private sealed record CcrSoapLogRow(
        string RequestUrl,
        Dictionary<string, StringValues> RequestHeaders,
        string RequestBody,
        HttpStatusCode ResponseStatusCode,
        Dictionary<string, StringValues> ResponseHeaders,
        string ResponseBody,
        TimeSpan Duration);

    private sealed class StringValuesJsonConverter
        : JsonConverter<StringValues>
    {
        public override bool HandleNull => true;

        public override StringValues Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return StringValues.Empty;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString()!;
                return value;
            }

            return JsonSerializer.Deserialize<string[]>(ref reader, options)!;
        }

        public override void Write(Utf8JsonWriter writer, StringValues value, JsonSerializerOptions options)
        {
            if (value.Count == 0)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Count == 1)
            {
                writer.WriteStringValue(value[0]);
                return;
            }

            writer.WriteStartArray();
            foreach (var v in value)
            {
                writer.WriteStringValue(v);
            }

            writer.WriteEndArray();
        }
    }
}
