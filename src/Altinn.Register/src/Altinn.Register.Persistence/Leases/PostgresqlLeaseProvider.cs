#nullable enable

using System.Data;
using System.Diagnostics;
using Altinn.Register.Core.Leases;
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
    public async Task<LeaseAcquireResult> TryAcquireLease(string leaseId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(leaseId);
        Guard.IsLessThan(duration, MAX_LEASE_DURATION);

        var now = _timeProvider.GetUtcNow();
        var expires = now + duration;
        var token = Guid.Empty;

        var result = await UpsertLease(leaseId, token, now, expires, cancellationToken);
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

        var result = await UpsertLease(leaseId, token, now, expires, cancellationToken);
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
    public async Task<bool> ReleaseLease(LeaseTicket lease, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(lease);

        var now = _timeProvider.GetUtcNow();
        var expires = DateTimeOffset.MinValue;
        var leaseId = lease.LeaseId;
        var token = lease.Token;

        var result = await UpsertLease(leaseId, token, now, expires, cancellationToken);
        var released = result.IsLeaseAcquired;

        if (released)
        {
            Log.LeaseReleased(_logger, leaseId);
        }

        return released;
    }

    private async Task<LeaseAcquireResult> UpsertLease(
        string leaseId,
        Guid token,
        DateTimeOffset now,
        DateTimeOffset expires,
        CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await _retryLeasePipeline.ExecuteAsync(
            callback: static (s, ct) => 
            {
                var (conn, leaseId, token, now, expires) = s;
                var task = UpsertLeaseInner(conn, leaseId, token, now, expires, ct);
                return new ValueTask<LeaseAcquireResult>(task);
            },
            state: (conn, leaseId, token, now, expires),
            cancellationToken: cancellationToken);

        static async Task<LeaseAcquireResult> UpsertLeaseInner(
            NpgsqlConnection conn,
            string leaseId,
            Guid token,
            DateTimeOffset now,
            DateTimeOffset expires,
            CancellationToken cancellationToken)
        {
            const string ACQUIRE_QUERY =
                /*strpsql*/"""
                INSERT INTO register.lease (id, token, expires)
                VALUES (@id, gen_random_uuid(), @expires)
                ON CONFLICT (id) DO UPDATE SET
                    token = EXCLUDED.token,
                    expires = EXCLUDED.expires
                    WHERE lease.token = @token OR lease.expires <= @now
                RETURNING id, token, expires
                """;

            Guard.IsNotNullOrEmpty(leaseId);

            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = ACQUIRE_QUERY;
            cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = leaseId;
            cmd.Parameters.Add<Guid>("token", NpgsqlDbType.Uuid).TypedValue = token;
            cmd.Parameters.Add<DateTimeOffset>("now", NpgsqlDbType.TimestampTz).TypedValue = now;
            cmd.Parameters.Add<DateTimeOffset>("expires", NpgsqlDbType.TimestampTz).TypedValue = expires;

            LeaseTicket? newLease = null;
            await cmd.PrepareAsync(cancellationToken);
            {
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    newLease = ReadLease(reader);
                }
            }

            if (newLease is null)
            {
                return await GetLeaseExpiry(conn, leaseId, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            return newLease;
        }

        static async Task<DateTimeOffset> GetLeaseExpiry(NpgsqlConnection conn, string id, CancellationToken cancellationToken)
        {
            const string GET_QUERY =
                /*strpsql*/"""
                SELECT id, token, expires
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

            return reader.GetFieldValue<DateTimeOffset>("expires");
        }

        static LeaseTicket ReadLease(NpgsqlDataReader reader)
        {
            var id = reader.GetFieldValue<string>("id");
            var token = reader.GetFieldValue<Guid>("token");
            var expires = reader.GetFieldValue<DateTimeOffset>("expires");

            return new LeaseTicket(id, token, expires);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Debug, "Failed to upsert lease due to serialization error, retrying...")]
        public static partial void FailedToUpsertLeaseDueToSerializationError(ILogger logger);
    }
}
