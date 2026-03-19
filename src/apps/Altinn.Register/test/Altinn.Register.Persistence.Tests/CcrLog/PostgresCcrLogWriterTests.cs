using System.Buffers;
using System.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Register.Persistence.CcrLog;
using Altinn.Register.TestsUtils.Npgsql;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Altinn.Register.Persistence.Tests.CcrLog;

public class PostgresCcrLogWriterTests
    : DatabaseTestBase
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new StringValuesJsonConverter(),
        },
    };

    private PostgresCcrLogWriter _sut = null!;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _sut = Services.GetRequiredService<PostgresCcrLogWriter>();
    }

    [Fact]
    public async Task LogCcrSoapRequest_InsertsLogEntry()
    {
        var id = TimeProvider.GetUuidV7();
        var requestStart = TimeProvider.GetUtcNow();
        var requestUrl = "https://example.com/soap-endpoint";
        var requestHeaders = EncodeHeaders([
            new("Content-Type", "application/soap+xml; charset=utf-8; action=https://test.example.com/test"),
            new("X-Custom-Multi", new(["foo", "bar"]))
        ]);
        var requestBody = EncodeBody("<soap:Envelope>...</soap:Envelope>");

        var responseStatusCode = HttpStatusCode.OK;
        var responseHeaders = EncodeHeaders([
            new("Content-Type", "application/soap+xml; charset=utf-8"),
        ]);
        var responseBody = EncodeBody("<soap:Envelope>...</soap:Envelope>");
        var duration = TimeSpan.FromSeconds(0.25);

        await _sut.LogCcrSoapRequest(
            id,
            requestStart,
            requestUrl,
            requestHeaders,
            requestBody,
            responseStatusCode,
            responseHeaders,
            responseBody,
            duration,
            CancellationToken);

        // Assert
        await using var conn = await Database.DataSource.OpenConnectionAsync(CancellationToken);
        await using var cmd = conn.CreateCommand(/*strpsql*/"SELECT * FROM register.ccr_soap_log l WHERE l.id = @id");
        cmd.Parameters.Add<Guid>("id", NpgsqlTypes.NpgsqlDbType.Uuid).TypedValue = id;

        await using var reader = await cmd.ExecuteReaderAsync(CancellationToken);
        (await reader.ReadAsync(CancellationToken)).ShouldBeTrue();

        reader.GetFieldValue<Guid>("id").ShouldBe(id);
        reader.GetFieldValue<DateTimeOffset>("request_start").ShouldBe(requestStart);
        reader.GetFieldValue<string>("request_url").ShouldBe(requestUrl);
        reader.GetJsonFieldValue<Dictionary<string, StringValues>>("request_headers", Options)
            .ShouldBeEquivalentTo(new Dictionary<string, StringValues>()
            {
                { "Content-Type", "application/soap+xml; charset=utf-8; action=https://test.example.com/test" },
                { "X-Custom-Multi", new(["foo", "bar"]) },
            });
        reader.GetFieldValue<string>("request_body").ShouldBe("<soap:Envelope>...</soap:Envelope>");

        reader.GetFieldValue<int>("response_http_status").ShouldBe((int)responseStatusCode);
        reader.GetJsonFieldValue<Dictionary<string, StringValues>>("response_headers", Options)
            .ShouldBeEquivalentTo(new Dictionary<string, StringValues>()
            {
                { "Content-Type", "application/soap+xml; charset=utf-8" },
            });
        reader.GetFieldValue<string>("response_body").ShouldBe("<soap:Envelope>...</soap:Envelope>");
        reader.GetFieldValue<TimeSpan>("duration").ShouldBe(duration);
    }

    private static ReadOnlySequence<byte> EncodeHeaders(IEnumerable<KeyValuePair<string, StringValues>> headers)
    {
        var dict = headers
            .GroupBy(
                h => h.Key,
                (key, values) => KeyValuePair.Create(key, values.Aggregate(StringValues.Empty, (acc, v) => StringValues.Concat(acc, v.Value))))
            .ToDictionary();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(dict, Options);
        return FromBytes(bytes);
    }

    public static ReadOnlySequence<byte> EncodeBody(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        return FromBytes(bytes);
    }

    private static ReadOnlySequence<byte> FromBytes(byte[] bytes)
        => new(bytes);

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
