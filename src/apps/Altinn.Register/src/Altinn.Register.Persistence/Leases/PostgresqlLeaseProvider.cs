#nullable enable

using System.Data;
using System.Diagnostics;
using Altinn.Authorization.ServiceDefaults.Leases;
using Altinn.Authorization.ServiceDefaults.Npgsql;
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
    private static readonly TimeSpan MIN_LEASE_DURATION = TimeSpan.FromSeconds(30);

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PostgresqlLeaseProvider> _logger;

    private readonly ResiliencePipeline<AcquireResult> _acquirePipeline;
    private readonly ResiliencePipeline<RenewResult> _rewnewPipeline;
    private readonly ResiliencePipeline<ReleaseResult> _releasePipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresqlLeaseProvider"/> class.
    /// <summary>
    /// Initializes a new PostgresqlLeaseProvider that stores leases in the provided PostgreSQL data source and uses the given time provider and logger.
    /// </summary>
    public PostgresqlLeaseProvider(
        NpgsqlDataSource dataSource, 
        TimeProvider timeProvider,
        ILogger<PostgresqlLeaseProvider> logger)
    {
        _dataSource = dataSource;
        _timeProvider = timeProvider;
        _logger = logger;

        _acquirePipeline = CreateRetryPipeline<AcquireResult>("acquire", timeProvider, logger);
        _rewnewPipeline = CreateRetryPipeline<RenewResult>("renew", timeProvider, logger);
        _releasePipeline = CreateRetryPipeline<ReleaseResult>("release", timeProvider, logger);
    }

    /// <summary>
    /// Attempts to acquire a lease with the specified id for the given duration.
    /// </summary>
    /// <param name="leaseId">The identifier of the lease to acquire.</param>
    /// <param name="duration">Requested lease duration; must be between the provider's minimum and maximum lease durations.</param>
    /// <param name="ifUnacquiredFor">
    /// Optional condition: if provided, the acquire will succeed only if the lease has been unacquired since (now - this value).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="LeaseAcquireResult"/> describing whether the lease was acquired and, when applicable, the lease token, expiry, and timestamps.
    /// </returns>
    public async Task<LeaseAcquireResult> TryAcquireLease(
        string leaseId,
        TimeSpan duration,
        TimeSpan? ifUnacquiredFor = null,
        CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT o.outcome, o.id, o.token, o.expires, o.acquired, o.released, o.condition
            FROM register.lease_acquire(@id, @now, @expires, @condition_released_before) AS o
            """;

        Guard.IsNotNullOrEmpty(leaseId);
        Guard.IsLessThan(duration, MAX_LEASE_DURATION);
        Guard.IsGreaterThan(duration, MIN_LEASE_DURATION);

        var now = _timeProvider.GetUtcNow();
        var expires = now + duration;
        var conditionReleasedBefore = ifUnacquiredFor.HasValue
            ? now - ifUnacquiredFor.Value
            : (DateTimeOffset?)null;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        var result = await _acquirePipeline.ExecuteAsync(
            callback: static async (s, cancellationToken) =>
            {
                var (self, conn, leaseId, now, expires, conditionReleasedBefore) = s;
                await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await using var cmd = conn.CreateCommand(QUERY);

                cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = leaseId;
                cmd.Parameters.Add<DateTimeOffset>("now", NpgsqlDbType.TimestampTz).TypedValue = now;
                cmd.Parameters.Add<DateTimeOffset>("expires", NpgsqlDbType.TimestampTz).TypedValue = expires;
                cmd.Parameters.Add<DateTimeOffset?>("condition_released_before", NpgsqlDbType.TimestampTz).TypedValue = conditionReleasedBefore;

                await cmd.PrepareAsync(cancellationToken);
                AcquireResult result;

                {
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var read = await reader.ReadAsync(cancellationToken);
                    Debug.Assert(read);

                    result = await AcquireResult.ReadAsync(reader, cancellationToken);
                }

                if (result is AcquireResult.Success)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                return result;
            },
            state: (this, conn, leaseId, now, expires, conditionReleasedBefore),
            cancellationToken: cancellationToken);

        Log.LeaseAcquireResult(_logger, result);
        return result.ToLeaseAcquireResult();
    }

    /// <summary>
    /// Attempts to renew an existing lease and returns the renewal outcome.
    /// </summary>
    /// <param name="lease">The lease ticket containing the lease id and current token to verify ownership.</param>
    /// <param name="duration">The requested renewal duration; must be between 30 seconds and 15 minutes.</param>
    /// <returns>
    /// A <see cref="LeaseAcquireResult"/> representing the renewal outcome: on success it contains the renewed lease (new token and expiry); otherwise it indicates why renewal failed (wrong token, lease not found, or lease expired).
    /// </returns>
    public async Task<LeaseAcquireResult> TryRenewLease(LeaseTicket lease, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT o.outcome, o.id, o.token, o.expires, o.acquired, o.released
            FROM register.lease_renew(@id, @token, @now, @expires) AS o
            """;

        Guard.IsNotNull(lease);
        Guard.IsLessThan(duration, MAX_LEASE_DURATION);
        Guard.IsGreaterThan(duration, MIN_LEASE_DURATION);

        var now = _timeProvider.GetUtcNow();
        var expires = now + duration;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        var result = await _rewnewPipeline.ExecuteAsync(
            callback: static async (s, cancellationToken) =>
            {
                var (self, conn, leaseId, token, now, expires) = s;
                await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await using var cmd = conn.CreateCommand(QUERY);

                cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = leaseId;
                cmd.Parameters.Add<Guid>("token", NpgsqlDbType.Uuid).TypedValue = token;
                cmd.Parameters.Add<DateTimeOffset>("now", NpgsqlDbType.TimestampTz).TypedValue = now;
                cmd.Parameters.Add<DateTimeOffset>("expires", NpgsqlDbType.TimestampTz).TypedValue = expires;

                await cmd.PrepareAsync(cancellationToken);
                RenewResult result;

                {
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var read = await reader.ReadAsync(cancellationToken);
                    Debug.Assert(read);

                    result = await RenewResult.ReadAsync(reader, cancellationToken);
                }

                if (result is RenewResult.Success)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                return result;
            },
            state: (this, conn, lease.LeaseId, lease.Token, now, expires),
            cancellationToken: cancellationToken);

        Log.LeaseRenewResult(_logger, result);
        return result.ToLeaseAcquireResult();
    }

    /// <summary>
    /// Releases the lease represented by the specified lease ticket and returns the outcome of the release attempt.
    /// </summary>
    /// <param name="lease">The lease ticket containing the lease identifier and token to validate and release.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the release operation.</param>
    /// <returns>A <see cref="LeaseReleaseResult"/> describing the result: success with release timestamps, or failure due to wrong token, lease not found, or lease expired.</returns>
    public async Task<LeaseReleaseResult> ReleaseLease(LeaseTicket lease, CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT o.outcome, o.id, o.expires, o.acquired, o.released
            FROM register.lease_release(@id, @token, @now) AS o
            """;

        Guard.IsNotNull(lease);
        var now = _timeProvider.GetUtcNow();

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        var result = await _releasePipeline.ExecuteAsync(
            callback: static async (s, cancellationToken) =>
            {
                var (self, conn, leaseId, token, now) = s;
                await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await using var cmd = conn.CreateCommand(QUERY);

                cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = leaseId;
                cmd.Parameters.Add<Guid>("token", NpgsqlDbType.Uuid).TypedValue = token;
                cmd.Parameters.Add<DateTimeOffset>("now", NpgsqlDbType.TimestampTz).TypedValue = now;

                await cmd.PrepareAsync(cancellationToken);
                ReleaseResult result;

                {
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var read = await reader.ReadAsync(cancellationToken);
                    Debug.Assert(read);

                    result = await ReleaseResult.ReadAsync(reader, cancellationToken);
                }

                if (result is ReleaseResult.Success)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                return result;
            },
            state: (this, conn, lease.LeaseId, lease.Token, now),
            cancellationToken: cancellationToken);

        Log.LeaseReleaseResult(_logger, result);
        return result.ToLeaseReleaseResult();
    }

    /// <summary>
    /// Creates a resilience pipeline that retries on PostgreSQL serialization failures for the specified lease action.
    /// </summary>
    /// <param name="actionName">Identifier for the lease action; included in retry log entries.</param>
    /// <param name="timeProvider">Time provider used by the pipeline for scheduling retries.</param>
    /// <param name="logger">Logger used to emit retry diagnostics.</param>
    /// <returns>A resilience pipeline configured to retry up to 3 times on serialization failures with a 10ms constant backoff and to log each retry attempt.</returns>
    private static ResiliencePipeline<T> CreateRetryPipeline<T>(string actionName, TimeProvider timeProvider, ILogger logger)
    {
        var pipelineBuilder = new ResiliencePipelineBuilder<T>();
        pipelineBuilder.TimeProvider = timeProvider;
        pipelineBuilder.AddRetry(new RetryStrategyOptions<T>
        {
            ShouldHandle = new PredicateBuilder<T>()
                .Handle<PostgresException>(e => e.SqlState == PostgresErrorCodes.SerializationFailure),
            BackoffType = DelayBackoffType.Constant,
            UseJitter = false,
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(10),
            OnRetry = args =>
            {
                Log.FailedToUpsertLeaseDueToSerializationError(logger, actionName, args.AttemptNumber);

                return ValueTask.CompletedTask;
            },
        });

        return pipelineBuilder.Build();
    }

    /// <summary>
    /// Throws an <see cref="UnreachableException"/> for an unexpected numeric outcome encountered when reading a result.
    /// </summary>
    /// <param name="resultName">The name of the result being read (used in the exception message).</param>
    /// <param name="outcome">The numeric outcome value that was not expected.</param>
    /// <exception cref="UnreachableException">Thrown when an outcome value is encountered that the code cannot handle.</exception>
    private static T UnreachableUtcome<T>(string resultName, short outcome)
    {
        throw new UnreachableException($"Unreachable outcome '{outcome}' encountered when reading {resultName}.");
    }

    private abstract record AcquireResult
    {
        /// <summary>
        /// Parses the current row from the provided data reader and constructs an <see cref="AcquireResult"/> that represents the outcome of an acquire attempt.
        /// </summary>
        /// <returns>
        /// An <see cref="AcquireResult"/> which is one of:
        /// - <see cref="AcquireResult.Success"/>: lease acquired with id, token, expires, and acquired timestamps;
        /// - <see cref="AcquireResult.ConditionUnmet"/>: acquire condition was not met, with the condition string and acquired/released timestamps;
        /// - <see cref="AcquireResult.LeaseUnavailable"/>: lease is currently held by another owner, with acquired and expires timestamps.
        /// </returns>
        /// <exception cref="UnreachableException">Thrown if the reader returns an outcome code that is not recognized.</exception>
        public static async ValueTask<AcquireResult> ReadAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
        {
            var outcome = await reader.GetFieldValueAsync<short>("outcome", cancellationToken);
            var id = await reader.GetFieldValueAsync<string>("id", cancellationToken);
            var token = await reader.GetFieldValueAsync<Guid?>("token", cancellationToken);
            var expires = await reader.GetFieldValueAsync<DateTimeOffset?>("expires", cancellationToken);
            var acquired = await reader.GetFieldValueAsync<DateTimeOffset?>("acquired", cancellationToken);
            var released = await reader.GetFieldValueAsync<DateTimeOffset?>("released", cancellationToken);
            var condition = await reader.GetFieldValueOrDefaultAsync<string>("condition", cancellationToken);

            Debug.Assert(id is not null);
            Debug.Assert(outcome is >= 0 and <= 2);

            switch (outcome)
            {
                // Success { id: text, token: uuid, expires: timestamp with time zone, acquired: timestamp with time zone }
                case 0:
                    Debug.Assert(token.HasValue);
                    Debug.Assert(expires.HasValue);
                    Debug.Assert(acquired.HasValue);
                    return new Success(id, token.Value, expires.Value, acquired.Value);

                // ConditionUnmet { id: text, condition: text, acquired: timestamp with time zone, released: timestamp with time zone }
                case 1:
                    Debug.Assert(!string.IsNullOrEmpty(condition));
                    Debug.Assert(acquired.HasValue);
                    Debug.Assert(released.HasValue);
                    return new ConditionUnmet(id, condition, acquired.Value, released.Value);

                // LeaseUnavailable { id: text, acquired: timestamp with time zone, expires: timestamp with time zone }
                case 2:
                    Debug.Assert(acquired.HasValue);
                    Debug.Assert(expires.HasValue);
                    return new LeaseUnavailable(id, acquired.Value, expires.Value);

                default:
                    return UnreachableUtcome<AcquireResult>(nameof(AcquireResult), outcome);
            }
        }

        /// <summary>
        /// Initializes an AcquireResult with the specified lease identifier.
        /// </summary>
        /// <param name="id">The lease identifier.</param>
        private AcquireResult(string id) 
        {
            Id = id;
        }

        public string Id { get; }

        /// <summary>
/// Convert this acquire operation result into a LeaseAcquireResult describing the acquired lease or the reason it failed.
/// </summary>
/// <returns>A LeaseAcquireResult that either contains the acquired lease id, token, and expiry on success, or contains the failure reason and any relevant timestamps (acquired, released, or expires) on failure.</returns>
public abstract LeaseAcquireResult ToLeaseAcquireResult();

        public sealed record Success(string Id, Guid Token, DateTimeOffset Expires, DateTimeOffset Acquired)
            : AcquireResult(Id)
        {
            /// <summary>
                /// Convert this successful renew result into a LeaseAcquireResult representing the acquired lease.
                /// </summary>
                /// <returns>A LeaseAcquireResult representing a successful acquisition with the associated LeaseTicket and acquisition time.</returns>
                public override LeaseAcquireResult ToLeaseAcquireResult()
                => LeaseAcquireResult.Acquired(new(Id, Token, Expires), Acquired, null);
        }

        public sealed record ConditionUnmet(string Id, string Condition, DateTimeOffset Acquired, DateTimeOffset Released)
            : AcquireResult(Id)
        {
            /// <summary>
                /// Maps this condition-unmet acquire outcome to a failed LeaseAcquireResult that carries the original acquired and released timestamps.
                /// </summary>
                /// <returns>A failed LeaseAcquireResult with the expiration set to <see cref="DateTimeOffset.MinValue"/> and the original acquired and released timestamps.</returns>
                public override LeaseAcquireResult ToLeaseAcquireResult()
                => LeaseAcquireResult.Failed(DateTimeOffset.MinValue, Acquired, Released);
        }
        
        public sealed record LeaseUnavailable(string Id, DateTimeOffset Acquired, DateTimeOffset Expires)
            : AcquireResult(Id)
        {
            /// <summary>
                /// Produce a LeaseAcquireResult indicating the renewal failed because the lease has expired.
                /// </summary>
                /// <returns>A LeaseAcquireResult representing a failed renewal with this lease's expiry and acquired timestamps and no new token.</returns>
                public override LeaseAcquireResult ToLeaseAcquireResult()
                => LeaseAcquireResult.Failed(Expires, Acquired, null);
        }
    }

    private abstract record RenewResult
    {
        /// <summary>
        /// Parses the current row from the provided NpgsqlDataReader into a <see cref="RenewResult"/> representing the outcome of a lease renewal operation.
        /// </summary>
        /// <param name="reader">A reader positioned on a row that contains the columns: "outcome", "id", "token", "expires", "acquired", and "released".</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous read operations.</param>
        /// <returns>
        /// A <see cref="RenewResult"/> instance:
        /// - <see cref="RenewResult.Success"/> when the lease was renewed (includes id, token, expires, acquired).
        /// - <see cref="RenewResult.WrongToken"/> when the provided token did not match (includes id, acquired, expires).
        /// - <see cref="RenewResult.LeaseNotFound"/> when no lease with the given id exists.
        /// - <see cref="RenewResult.LeaseExpired"/> when the lease has already expired (includes id, acquired, expires).
        /// </returns>
        public static async ValueTask<RenewResult> ReadAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
        {
            var outcome = await reader.GetFieldValueAsync<short>("outcome", cancellationToken);
            var id = await reader.GetFieldValueAsync<string>("id", cancellationToken);
            var token = await reader.GetFieldValueAsync<Guid?>("token", cancellationToken);
            var expires = await reader.GetFieldValueAsync<DateTimeOffset?>("expires", cancellationToken);
            var acquired = await reader.GetFieldValueAsync<DateTimeOffset?>("acquired", cancellationToken);
            var released = await reader.GetFieldValueAsync<DateTimeOffset?>("released", cancellationToken);

            Debug.Assert(id is not null);
            Debug.Assert(outcome is >= 0 and <= 3);

            switch (outcome)
            {
                // Success { id: text, token: uuid, expires: timestamp with time zone, acquired: timestamp with time zone }
                case 0:
                    Debug.Assert(token.HasValue);
                    Debug.Assert(expires.HasValue);
                    Debug.Assert(acquired.HasValue);
                    return new Success(id, token.Value, expires.Value, acquired.Value);

                // WrongToken { id: text, acquired: timestamp with time zone, expires: timestamp with time zone }
                case 1:
                    Debug.Assert(acquired.HasValue);
                    Debug.Assert(expires.HasValue);
                    return new WrongToken(id, acquired.Value, expires.Value);

                // LeaseNotFound { id: text }
                case 2:
                    return new LeaseNotFound(id);

                // LeaseExpired { id: text, acquired: timestamp with time zone, expires: timestamp with time zone }
                case 3:
                    Debug.Assert(acquired.HasValue);
                    Debug.Assert(expires.HasValue);
                    return new LeaseExpired(id, acquired.Value, expires.Value);

                default:
                    return UnreachableUtcome<RenewResult>(nameof(RenewResult), outcome);
            }
        }

        /// <summary>
        /// Creates a RenewResult for the specified lease identifier.
        /// </summary>
        /// <param name="id">The lease identifier.</param>
        private RenewResult(string id)
        {
            Id = id;
        }

        public string Id { get; }

        /// <summary>
/// Convert this acquire operation result into a LeaseAcquireResult describing the acquired lease or the reason it failed.
/// </summary>
/// <returns>A LeaseAcquireResult that either contains the acquired lease id, token, and expiry on success, or contains the failure reason and any relevant timestamps (acquired, released, or expires) on failure.</returns>
public abstract LeaseAcquireResult ToLeaseAcquireResult();

        public sealed record Success(string Id, Guid Token, DateTimeOffset Expires, DateTimeOffset Acquired)
            : RenewResult(Id)
        {
            /// <summary>
                /// Convert this successful renew result into a LeaseAcquireResult representing the acquired lease.
                /// </summary>
                /// <returns>A LeaseAcquireResult representing a successful acquisition with the associated LeaseTicket and acquisition time.</returns>
                public override LeaseAcquireResult ToLeaseAcquireResult()
                => LeaseAcquireResult.Acquired(new(Id, Token, Expires), Acquired, null);
        }

        public sealed record WrongToken(string Id, DateTimeOffset Acquired, DateTimeOffset Expires)
            : RenewResult(Id)
        {
            /// <summary>
                /// Produce a LeaseAcquireResult indicating the renewal failed because the lease has expired.
                /// </summary>
                /// <returns>A LeaseAcquireResult representing a failed renewal with this lease's expiry and acquired timestamps and no new token.</returns>
                public override LeaseAcquireResult ToLeaseAcquireResult()
                => LeaseAcquireResult.Failed(Expires, Acquired, null);
        }

        public sealed record LeaseNotFound(string Id)
            : RenewResult(Id)
        {
            /// <summary>
                /// Converts this result into a failed LeaseAcquireResult with no acquisition timestamp, token, or expiry information.
                /// </summary>
                /// <returns>A <see cref="LeaseAcquireResult"/> indicating failure; acquisition time is <see cref="DateTimeOffset.MinValue"/> and token and expiry are null.</returns>
                public override LeaseAcquireResult ToLeaseAcquireResult()
                => LeaseAcquireResult.Failed(DateTimeOffset.MinValue, null, null);
        }

        public sealed record LeaseExpired(string Id, DateTimeOffset Acquired, DateTimeOffset Expires)
            : RenewResult(Id)
        {
            /// <summary>
                /// Produce a LeaseAcquireResult indicating the renewal failed because the lease has expired.
                /// </summary>
                /// <returns>A LeaseAcquireResult representing a failed renewal with this lease's expiry and acquired timestamps and no new token.</returns>
                public override LeaseAcquireResult ToLeaseAcquireResult()
                => LeaseAcquireResult.Failed(Expires, Acquired, null);
        }
    }

    private abstract record ReleaseResult
    {
        /// <summary>
        /// Parses a <see cref="ReleaseResult"/> from the current row of the provided <see cref="NpgsqlDataReader"/>.
        /// </summary>
        /// <param name="reader">The data reader positioned on the row containing release result fields.</param>
        /// <param name="cancellationToken">Token to observe while performing asynchronous reads.</param>
        /// <returns>
        /// A <see cref="ReleaseResult"/> value representing the parsed outcome: <see cref="ReleaseResult.Success"/>, <see cref="ReleaseResult.WrongToken"/>, <see cref="ReleaseResult.LeaseNotFound"/>, or <see cref="ReleaseResult.LeaseExpired"/>.
        /// </returns>
        public static async ValueTask<ReleaseResult> ReadAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
        {
            var outcome = await reader.GetFieldValueAsync<short>("outcome", cancellationToken);
            var id = await reader.GetFieldValueAsync<string>("id", cancellationToken);
            var expires = await reader.GetFieldValueAsync<DateTimeOffset?>("expires", cancellationToken);
            var acquired = await reader.GetFieldValueAsync<DateTimeOffset?>("acquired", cancellationToken);
            var released = await reader.GetFieldValueAsync<DateTimeOffset?>("released", cancellationToken);

            Debug.Assert(id is not null);
            Debug.Assert(outcome is >= 0 and <= 3);

            switch (outcome)
            {
                // Success { id: text, acquired: timestamp with time zone, released: timestamp with time zone }
                case 0:
                    Debug.Assert(acquired.HasValue);
                    Debug.Assert(released.HasValue);
                    return new Success(id, acquired.Value, released.Value);

                // WrongToken { id: text, acquired: timestamp with time zone, expires: timestamp with time zone }
                case 1:
                    Debug.Assert(acquired.HasValue);
                    Debug.Assert(expires.HasValue);
                    return new WrongToken(id, acquired.Value, expires.Value);

                // LeaseNotFound { id: text }
                case 2:
                    return new LeaseNotFound(id);

                // LeaseExpired { id: text, acquired: timestamp with time zone, expires: timestamp with time zone }
                case 3:
                    Debug.Assert(acquired.HasValue);
                    Debug.Assert(expires.HasValue);
                    return new LeaseExpired(id, acquired.Value, expires.Value);

                default:
                    return UnreachableUtcome<ReleaseResult>(nameof(ReleaseResult), outcome);
            }
        }

        /// <summary>
        /// Initializes a <see cref="ReleaseResult"/> with the specified lease identifier.
        /// </summary>
        /// <param name="id">The lease identifier.</param>
        private ReleaseResult(string id)
        {
            Id = id;
        }

        public string Id { get; }

        /// <summary>
/// Map this ReleaseResult to a LeaseReleaseResult describing the final release outcome.
/// </summary>
/// <returns>A LeaseReleaseResult describing the outcome: Success includes acquired and released timestamps; WrongToken indicates a token mismatch and the lease's expiry; LeaseNotFound indicates no matching lease; LeaseExpired indicates the lease had already expired with its acquired and expiry timestamps.</returns>
public abstract LeaseReleaseResult ToLeaseReleaseResult();

        public sealed record Success(string Id, DateTimeOffset Acquired, DateTimeOffset Released)
            : ReleaseResult(Id)
        {
            /// <summary>
                /// Maps this success release outcome to a LeaseReleaseResult that indicates the lease was released and carries the relevant timestamps.
                /// </summary>
                /// <returns>LeaseReleaseResult with IsReleased = true, Expires = DateTimeOffset.MinValue, LastAcquiredAt set to the original acquired time, and LastReleasedAt set to the release time.</returns>
                public override LeaseReleaseResult ToLeaseReleaseResult()
                => new LeaseReleaseResult { IsReleased = true, Expires = DateTimeOffset.MinValue, LastAcquiredAt = Acquired, LastReleasedAt = Released };
        }

        public sealed record WrongToken(string Id, DateTimeOffset Acquired, DateTimeOffset Expires)
            : ReleaseResult(Id)
        {
            /// <summary>
                /// Convert this release outcome into a LeaseReleaseResult that indicates the lease was not released.
                /// </summary>
                /// <returns>
                /// A <see cref="LeaseReleaseResult"/> with <c>IsReleased</c> set to <c>false</c>, <c>Expires</c> set to this result's expiration, <c>LastAcquiredAt</c> set to this result's acquired time, and <c>LastReleasedAt</c> set to <c>null</c>.
                /// </returns>
                public override LeaseReleaseResult ToLeaseReleaseResult()
                => new LeaseReleaseResult { IsReleased = false, Expires = Expires, LastAcquiredAt = Acquired, LastReleasedAt = null };
        }

        public sealed record LeaseNotFound(string Id)
            : ReleaseResult(Id)
        {
            /// <summary>
                /// Maps this release outcome to a LeaseReleaseResult that indicates the lease was not released.
                /// </summary>
                /// <returns>A <see cref="LeaseReleaseResult"/> with IsReleased set to <c>false</c>, Expires set to <see cref="DateTimeOffset.MinValue"/>, and both LastAcquiredAt and LastReleasedAt set to <c>null</c>.</returns>
                public override LeaseReleaseResult ToLeaseReleaseResult()
                => new LeaseReleaseResult { IsReleased = false, Expires = DateTimeOffset.MinValue, LastAcquiredAt = null, LastReleasedAt = null };
        }

        public sealed record LeaseExpired(string Id, DateTimeOffset Acquired, DateTimeOffset Expires)
            : ReleaseResult(Id)
        {
            /// <summary>
                /// Convert this release outcome into a LeaseReleaseResult that indicates the lease was not released.
                /// </summary>
                /// <returns>
                /// A <see cref="LeaseReleaseResult"/> with <c>IsReleased</c> set to <c>false</c>, <c>Expires</c> set to this result's expiration, <c>LastAcquiredAt</c> set to this result's acquired time, and <c>LastReleasedAt</c> set to <c>null</c>.
                /// </returns>
                public override LeaseReleaseResult ToLeaseReleaseResult()
                => new LeaseReleaseResult { IsReleased = false, Expires = Expires, LastAcquiredAt = Acquired, LastReleasedAt = null };
        }
    }

    private static partial class Log
    {
        /// <summary>
        /// Logs a debug message when a lease operation fails due to a PostgreSQL serialization error and will be retried.
        /// </summary>
        /// <param name="action">Name of the lease action (for example "acquire", "renew", or "release").</param>
        /// <param name="attemptNumber">The current retry attempt number (1-based).</param>
        [LoggerMessage(0, LogLevel.Debug, "Failed to {Action} lease due to serialization error. Attempt = {AttemptNumber}")]
        public static partial void FailedToUpsertLeaseDueToSerializationError(ILogger logger, string action, int attemptNumber);

        /// <summary>
        /// Logs the result of an attempt to acquire the specified lease.
        /// </summary>
        /// <param name="leaseId">Identifier of the lease.</param>
        /// <param name="result">Outcome of the acquire operation.</param>
        [LoggerMessage(6, LogLevel.Debug, "Lease {LeaseId} acquire result: {Result}")]
        private static partial void LeaseAcquireResult(ILogger logger, string leaseId, AcquireResult result);
        
        /// <summary>
/// Log the outcome of a lease acquire operation.
/// </summary>
/// <param name="logger">The logger to write the message to.</param>
/// <param name="result">The acquire result containing the lease id and outcome.</param>
public static void LeaseAcquireResult(ILogger logger, AcquireResult result) => LeaseAcquireResult(logger, result.Id, result);

        /// <summary>
        /// Logs the outcome of a lease renewal operation at Debug level.
        /// </summary>
        /// <param name="leaseId">The identifier of the lease being renewed.</param>
        /// <param name="result">The renewal result details to include in the log message.</param>
        [LoggerMessage(7, LogLevel.Debug, "Lease {LeaseId} renew result: {Result}")]
        private static partial void LeaseRenewResult(ILogger logger, string leaseId, RenewResult result);

        /// <summary>
/// Log the outcome of a lease renewal operation.
/// </summary>
/// <param name="result">The renewal result to log; contains the lease identifier and outcome details.</param>
public static void LeaseRenewResult(ILogger logger, RenewResult result) => LeaseRenewResult(logger, result.Id, result);

        /// <summary>
        /// Logs the result of a lease release operation.
        /// </summary>
        /// <param name="leaseId">Identifier of the lease.</param>
        /// <param name="result">The release outcome represented by a <see cref="ReleaseResult"/> instance.</param>
        [LoggerMessage(8, LogLevel.Debug, "Lease {LeaseId} release result: {Result}")]
        private static partial void LeaseReleaseResult(ILogger logger, string leaseId, ReleaseResult result);

        /// <summary>
/// Logs the outcome of a lease release operation.
/// </summary>
/// <param name="result">The release result whose outcome and lease id are logged.</param>
public static void LeaseReleaseResult(ILogger logger, ReleaseResult result) => LeaseReleaseResult(logger, result.Id, result);
    }
}