using System.Buffers;
using System.Net;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Register.Core.CcrLog;
using Nerdbank.Streams;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence.CcrLog;

/// <summary>
/// Postgres backed implementation of <see cref="ICcrLogWriter"/>.
/// </summary>
internal class PostgresCcrLogWriter
    : ICcrLogWriter
{
    private readonly NpgsqlDataSource _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresCcrLogWriter"/> class.
    /// </summary>
    public PostgresCcrLogWriter(NpgsqlDataSource db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task LogCcrSoapRequest(
        Guid id,
        DateTimeOffset requestStart,
        string requestUrl,
        ReadOnlySequence<byte> requestHeaders,
        ReadOnlySequence<byte> requestBody,
        HttpStatusCode responseStatusCode,
        ReadOnlySequence<byte> responseHeaders,
        ReadOnlySequence<byte> responseBody,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            INSERT INTO register.ccr_soap_log(id, request_start, request_url, request_headers, request_body, response_http_status, response_headers, response_body, duration)
            VALUES (@id, @request_start, @request_url, @request_headers, @request_body, @response_http_status, @response_headers, @response_body, @duration)
            """;

        await using var conn = await _db.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand(QUERY);

        cmd.Parameters.Add<Guid>("id", NpgsqlDbType.Uuid).TypedValue = id;
        cmd.Parameters.Add<DateTimeOffset>("request_start", NpgsqlDbType.TimestampTz).TypedValue = requestStart;
        cmd.Parameters.Add<string>("request_url", NpgsqlDbType.Text).TypedValue = requestUrl;
        cmd.Parameters.Add<Stream>("request_headers", NpgsqlDbType.Jsonb).TypedValue = requestHeaders.AsStream();
        cmd.Parameters.Add<Stream>("request_body", NpgsqlDbType.Text).TypedValue = requestBody.AsStream();
        cmd.Parameters.Add<int>("response_http_status", NpgsqlDbType.Integer).TypedValue = (int)responseStatusCode;
        cmd.Parameters.Add<Stream>("response_headers", NpgsqlDbType.Jsonb).TypedValue = responseHeaders.AsStream();
        cmd.Parameters.Add<Stream>("response_body", NpgsqlDbType.Text).TypedValue = responseBody.AsStream();
        cmd.Parameters.Add<TimeSpan>("duration", NpgsqlDbType.Interval).TypedValue = duration;

        await cmd.PrepareAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
