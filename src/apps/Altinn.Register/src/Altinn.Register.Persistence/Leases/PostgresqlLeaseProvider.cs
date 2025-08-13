#nullable enable

using System.Data;
using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Authorization.ServiceDefaults.Leases;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.Retry;

namespace Altinn.Register.Persistence.Leases;

/// <summary>
/// Implementation of <see cref="ILeaseProvider"/> that uses a postgresql database
/// as lease storage.
/// </summary>
internal partial class PostgresqlLeaseProvider
    : ILeaseProvider
{
    private static readonly TimeSpan MAX_LEASE_DURATION = TimeSpan.FromMinutes(15);

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PostgresqlLeaseProvider> _logger;
    private readonly ResiliencePipeline<LeaseAcquireResult> _retryLeasePipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresqlLeaseProvider"/> class.
    /// </summary>
    public PostgresqlLeaseProvider(
        NpgsqlDataSource dataSource, 
        TimeProvider timeProvider,
        ILogger<PostgresqlLeaseProvider> logger)
    {
        _dataSource = dataSource;
        _timeProvider = timeProvider;
        _logger = logger;

        var pipelineBuilder = new ResiliencePipelineBuilder<LeaseAcquireResult>();
        pipelineBuilder.TimeProvider = timeProvider;
        pipelineBuilder.AddRetry(new RetryStrategyOptions<LeaseAcquireResult>
        {
            ShouldHandle = new PredicateBuilder<LeaseAcquireResult>()
                .Handle<PostgresException>(e => e.SqlState == PostgresErrorCodes.SerializationFailure),
            BackoffType = DelayBackoffType.Constant,
            UseJitter = false,
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(10),
            OnRetry = args =>
            {
                Log.FailedToUpsertLeaseDueToSerializationError(logger);
                
                return ValueTask.CompletedTask;
            },
        });

        _retryLeasePipeline = pipelineBuilder.Build();
    }

    /// <inheritdoc/>
    public async Task<LeaseAcquireResult> TryAcquireLease(
        string leaseId,
        TimeSpan duration,
        Func<LeaseInfo, bool>? filter = null,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(leaseId);
        Guard.IsLessThan(duration, MAX_LEASE_DURATION);

        var now = _timeProvider.GetUtcNow();
        var expires = now + duration;
        var token = Guid.Empty;

        var result = await UpsertLease(
            new LeaseUpsert
            {
                LeaseId = leaseId,
                Token = token,
                Now = now,
                Expires = expires,
                Acquired = now,
                Released = FieldValue.Unset,
                Filter = filter,
            },
            cancellationToken);

        if (result.IsLeaseAcquired)
        {
            Log.LeaseAcquired(_logger, result.Lease.LeaseId);
        }
        else
        {
            Log.LeaseNotAcquiredAlreadyHeld(_logger, leaseId);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<LeaseAcquireResult> TryRenewLease(LeaseTicket lease, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(lease);
        Guard.IsLessThan(duration, MAX_LEASE_DURATION);

        var now = _timeProvider.GetUtcNow();
        var expires = now + duration;
        var leaseId = lease.LeaseId;
        var token = lease.Token;

        var result = await UpsertLease(
            new LeaseUpsert
            {
                LeaseId = leaseId,
                Token = token,
                Now = now,
                Expires = expires,
                Acquired = FieldValue.Unset,
                Released = FieldValue.Unset,
            },
            cancellationToken);

        if (result.IsLeaseAcquired) 
        {
            Log.LeaseRenewed(_logger, result.Lease.LeaseId);
        }
        else
        {
            Log.LeaseNotAcquiredAlreadyHeld(_logger, leaseId);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<LeaseReleaseResult> ReleaseLease(LeaseTicket lease, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(lease);

        var now = _timeProvider.GetUtcNow();
        var expires = DateTimeOffset.MinValue;
        var leaseId = lease.LeaseId;
        var token = lease.Token;

        var result = await UpsertLease(
            new LeaseUpsert
            {
                LeaseId = leaseId,
                Token = token,
                Now = now,
                Expires = expires,
                Acquired = FieldValue.Unset,
                Released = now,
            },
            cancellationToken);

        var released = result.IsLeaseAcquired;

        if (released)
        {
            Log.LeaseReleased(_logger, leaseId);
        }

        return new LeaseReleaseResult
        {
            IsReleased = released,
            Expires = result.Expires,
            LastAcquiredAt = result.LastAcquiredAt,
            LastReleasedAt = result.LastReleasedAt,
        };
    }

    private async Task<LeaseAcquireResult> UpsertLease(
        LeaseUpsert upsert,
        CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await _retryLeasePipeline.ExecuteAsync(
            callback: static (s, ct) => 
            {
                var (conn, upsert) = s;
                var task = UpsertLeaseInner(conn, upsert, ct);
                return new ValueTask<LeaseAcquireResult>(task);
            },
            state: (conn, upsert),
            cancellationToken: cancellationToken);

        static async Task<LeaseAcquireResult> UpsertLeaseInner(
            NpgsqlConnection conn,
            LeaseUpsert upsert,
            CancellationToken cancellationToken)
        {
            const string ACQUIRE_QUERY =
                /*strpsql*/"""
                WITH original AS (
                    SELECT id, expires, acquired, released
                    FROM register.lease
                    WHERE id = @id
                ), updated AS (
                    INSERT INTO register.lease (id, token, expires, acquired, released)
                    VALUES (@id, gen_random_uuid(), @expires, @acquired, @released)
                    ON CONFLICT (id) DO UPDATE SET
                        token = EXCLUDED.token,
                        expires = EXCLUDED.expires,
                        acquired = CASE @has_acquired WHEN true THEN EXCLUDED.acquired ELSE lease.acquired END,
                        released = CASE @has_released WHEN true THEN EXCLUDED.released ELSE lease.released END
                        WHERE lease.token = @token OR lease.expires <= @now
                    RETURNING id, token, expires, acquired, released
                )
                SELECT 
                    updated.id,
                    updated.token,
                    updated.expires,
                    updated.acquired,
                    updated.released,
                    original.expires AS prev_expires,
                    original.acquired AS prev_acquired,
                    original.released AS prev_released
                FROM updated
                LEFT JOIN original ON updated.id = original.id
                """;

            Guard.IsNotNullOrEmpty(upsert.LeaseId);

            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = ACQUIRE_QUERY;
            cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = upsert.LeaseId;
            cmd.Parameters.Add<Guid>("token", NpgsqlDbType.Uuid).TypedValue = upsert.Token;
            cmd.Parameters.Add<DateTimeOffset>("now", NpgsqlDbType.TimestampTz).TypedValue = upsert.Now;
            cmd.Parameters.Add<DateTimeOffset>("expires", NpgsqlDbType.TimestampTz).TypedValue = upsert.Expires;
            cmd.Parameters.Add<DateTimeOffset?>("acquired", NpgsqlDbType.TimestampTz).TypedValue = upsert.Acquired.HasValue ? upsert.Acquired.Value : null;
            cmd.Parameters.Add<DateTimeOffset?>("released", NpgsqlDbType.TimestampTz).TypedValue = upsert.Released.HasValue ? upsert.Released.Value : null;
            cmd.Parameters.Add<bool>("has_acquired", NpgsqlDbType.Boolean).TypedValue = !upsert.Acquired.IsUnset;
            cmd.Parameters.Add<bool>("has_released", NpgsqlDbType.Boolean).TypedValue = !upsert.Released.IsUnset;

            LeaseAcquireResult? result = null;
            await cmd.PrepareAsync(cancellationToken);
            {
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    if (upsert.Filter is null || upsert.Filter(await ReadLeaseInfo(reader, cancellationToken)))
                    {
                        result = await ReadLease(reader, cancellationToken);
                    } 
                    else
                    {
                        var expires = await reader.GetFieldValueAsync<DateTimeOffset?>("prev_expires", cancellationToken);
                        var lastAcquiredAt = await reader.GetFieldValueAsync<DateTimeOffset?>("prev_acquired", cancellationToken);
                        var lastReleasedAt = await reader.GetFieldValueAsync<DateTimeOffset?>("prev_released", cancellationToken);

                        // returning here rolls back the transaction
                        return LeaseAcquireResult.Failed(
                            expires ?? DateTimeOffset.MinValue,
                            lastAcquiredAt: lastAcquiredAt,
                            lastReleasedAt: lastReleasedAt);
                    }
                }
            }

            if (result is null)
            {
                // returning here rolls back the transaction
                return await GetFailedLeaseResult(conn, upsert.LeaseId, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            return result;
        }

        static async Task<LeaseAcquireResult> GetFailedLeaseResult(NpgsqlConnection conn, string id, CancellationToken cancellationToken)
        {
            const string GET_QUERY =
                /*strpsql*/"""
                SELECT id, expires, acquired, released
                FROM register.lease
                WHERE id = @id
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = GET_QUERY;
            cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new UnreachableException("Lease was not acquired, but no lease was found in the database");
            }

            var expires = await reader.GetFieldValueAsync<DateTimeOffset>("expires", cancellationToken);
            var lastAcquiredAt = await reader.GetFieldValueAsync<DateTimeOffset?>("acquired", cancellationToken);
            var lastReleasedAt = await reader.GetFieldValueAsync<DateTimeOffset?>("released", cancellationToken);

            return LeaseAcquireResult.Failed(expires, lastAcquiredAt: lastAcquiredAt, lastReleasedAt: lastReleasedAt);
        }

        static async ValueTask<LeaseInfo> ReadLeaseInfo(NpgsqlDataReader reader, CancellationToken cancellationToken)
        {
            var id = await reader.GetFieldValueAsync<string>("id", cancellationToken);
            var acquired = await reader.GetFieldValueAsync<DateTimeOffset?>("prev_acquired", cancellationToken);
            var released = await reader.GetFieldValueAsync<DateTimeOffset?>("prev_released", cancellationToken);

            return new LeaseInfo
            {
                LeaseId = id,
                LastAcquiredAt = acquired,
                LastReleasedAt = released
            };
        }

        static async ValueTask<LeaseAcquireResult> ReadLease(NpgsqlDataReader reader, CancellationToken cancellationToken)
        {
            var id = await reader.GetFieldValueAsync<string>("id", cancellationToken);
            var token = await reader.GetFieldValueAsync<Guid>("token", cancellationToken);
            var expires = await reader.GetFieldValueAsync<DateTimeOffset>("expires", cancellationToken);
            var lastAcquiredAt = await reader.GetFieldValueAsync<DateTimeOffset?>("acquired", cancellationToken);
            var lastReleasedAt = await reader.GetFieldValueAsync<DateTimeOffset?>("released", cancellationToken);

            var lease = new LeaseTicket(id, token, expires);
            return LeaseAcquireResult.Acquired(lease, lastAcquiredAt: lastAcquiredAt, lastReleasedAt: lastReleasedAt);
        }
    }

    private readonly record struct LeaseUpsert
    {
        public required string LeaseId { get; init; }

        public required Guid Token { get; init; }

        public required DateTimeOffset Now { get; init; }

        public required DateTimeOffset Expires { get; init; }

        public required FieldValue<DateTimeOffset> Acquired { get; init; }

        public required FieldValue<DateTimeOffset> Released { get; init; }

        public Func<LeaseInfo, bool>? Filter { get; init; }
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Debug, "Failed to upsert lease due to serialization error, retrying...")]
        public static partial void FailedToUpsertLeaseDueToSerializationError(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Lease {LeaseId} acquired")]
        public static partial void LeaseAcquired(ILogger logger, string leaseId);

        [LoggerMessage(2, LogLevel.Debug, "Lease {LeaseId} renewed")]
        public static partial void LeaseRenewed(ILogger logger, string leaseId);

        [LoggerMessage(3, LogLevel.Debug, "Lease {LeaseId} released")]
        public static partial void LeaseReleased(ILogger logger, string leaseId);

        [LoggerMessage(4, LogLevel.Debug, "Lease {LeaseId} was not acquired as it is already held")]
        public static partial void LeaseNotAcquiredAlreadyHeld(ILogger logger, string leaseId);
    }
}
