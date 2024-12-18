using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Altinn.Register.Core;
using Altinn.Register.Core.ImportJobs;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.Retry;

namespace Altinn.Register.Persistence.ImportJobs;

/// <summary>
/// Postgresql backed implementation of <see cref="IImportJobTracker"/>.
/// </summary>
internal partial class PostgresImportJobTracker
    : IImportJobTracker
{
    private static readonly ResiliencePropertyKey<Activity?> ActivityPropertyKey = new($"{nameof(PostgresImportJobTracker)}.{nameof(Activity)}");

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresImportJobTracker> _logger;

    /// <remarks>
    /// This class is designed to be a singleton due to the <see cref="_retryPipeline"/>
    /// being expensive to create. If for whatever reason this class needs to be registered
    /// as a transient/scoped service, the <see cref="_retryPipeline"/> should be moved to
    /// a separate singleton (nested) class and injected as a dependency.
    /// </remarks>
    private readonly ResiliencePipeline _retryPipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresImportJobTracker"/> class.
    /// </summary>
    public PostgresImportJobTracker(
        NpgsqlDataSource dataSource,
        TimeProvider timeProvider,
        ILogger<PostgresImportJobTracker> logger)
    {
        _dataSource = dataSource;
        _logger = logger;

        var pipelineBuilder = new ResiliencePipelineBuilder();
        pipelineBuilder.TimeProvider = timeProvider;
        pipelineBuilder.AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<JobDoesNotExistException>()
                .Handle<PostgresException>(static e =>
                {
                    if (e.SqlState == PostgresErrorCodes.SerializationFailure)
                    {
                        // retry all serialization failures
                        return true;
                    }

                    if (e.SqlState == PostgresErrorCodes.CheckViolation)
                    {
                        // if the constraint violated is due to the updated value being greater than the source value, retry
                        // assuming the source value has been updated since the last read
                        return e.ConstraintName is "enqueued_max_less_than_or_equal_to_source_max" or "processed_max_less_than_or_equal_to_enqueued_max";
                    }

                    return false;
                }),
            BackoffType = DelayBackoffType.Constant,
            UseJitter = false,
            MaxRetryAttempts = 3,
            DelayGenerator = static args =>
            {
                if (args.Outcome.Exception is PostgresException e
                    && e.SqlState == PostgresErrorCodes.CheckViolation)
                {
                    // all check violations are generally due to race conditions, so delay a shot while before retrying
                    return new(TimeSpan.FromMilliseconds(10));
                }

                return new(TimeSpan.Zero);
            },
            OnRetry = args =>
            {
                var activity = args.Context.Properties.GetValue(ActivityPropertyKey, defaultValue: null);
                activity?.SetTag("retry.count", args.AttemptNumber);
                Log.TransactionSerializationError(logger);

                return ValueTask.CompletedTask;
            },
        });

        _retryPipeline = pipelineBuilder.Build();
    }

    /// <inheritdoc/>
    public Task<ImportJobStatus> GetStatus(string id, CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            SELECT source_max, enqueued_max, processed_max
            FROM register.import_job
            WHERE id = @id
            """;

        return WithTransaction(
            id,
            static async (id, conn, logger, cancellationToken) =>
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = QUERY;

                cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;

                await cmd.PrepareAsync(cancellationToken);
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                {
                    // Job is not yet created
                    return default;
                }

                var sourceMax = reader.GetFieldValue<long>("source_max");
                var enqueuedMax = reader.GetFieldValue<long>("enqueued_max");
                var processedMax = reader.GetFieldValue<long>("processed_max");

                return new ImportJobStatus
                {
                    SourceMax = (ulong)sourceMax,
                    EnqueuedMax = (ulong)enqueuedMax,
                    ProcessedMax = (ulong)processedMax,
                };
            },
            _logger,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task TrackQueueStatus(string id, ImportJobQueueStatus status, CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            INSERT INTO register.import_job (id, source_max, enqueued_max, processed_max)
            VALUES (@id, @source_max, @enqueued_max, 0)
            ON CONFLICT (id) DO UPDATE
                SET source_max = GREATEST(import_job.source_max, EXCLUDED.source_max),
                    enqueued_max = GREATEST(import_job.enqueued_max, EXCLUDED.enqueued_max)
                WHERE import_job.enqueued_max < EXCLUDED.enqueued_max
                    OR import_job.source_max < EXCLUDED.source_max
            """;

        return WithTransaction(
            id,
            static async (id, conn, state, cancellationToken) =>
            {
                var (status, logger) = state;
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = QUERY;

                cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;
                cmd.Parameters.Add<long>("source_max", NpgsqlDbType.Bigint).TypedValue = checked((long)status.SourceMax);
                cmd.Parameters.Add<long>("enqueued_max", NpgsqlDbType.Bigint).TypedValue = checked((long)status.EnqueuedMax);

                Log.UpdateQueueStatus(logger, id, status);
                await cmd.PrepareAsync(cancellationToken);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            },
            new WithLoggerState<ImportJobQueueStatus>(status, _logger),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> TrackProcessedStatus(string id, ImportJobProcessingStatus status, CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            WITH existing AS (
                SELECT 1
                FROM register.import_job
                WHERE id = @id
            ), updated AS (
                UPDATE register.import_job
                SET processed_max = @processed_max
                WHERE id = @id AND processed_max < @processed_max
                RETURNING 1
            )
            SELECT 
                EXISTS (SELECT 1 FROM existing) existing,
                EXISTS (SELECT 1 FROM updated) updated
            """;

        return WithTransaction(
            id,
            static async (id, conn, state, cancellationToken) =>
            {
                var (status, logger) = state;
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = QUERY;

                cmd.Parameters.Add<string>("id", NpgsqlDbType.Text).TypedValue = id;
                cmd.Parameters.Add<long>("processed_max", NpgsqlDbType.Bigint).TypedValue = checked((long)status.ProcessedMax);

                Log.UpdateProcessedStatus(logger, id, status);
                await cmd.PrepareAsync(cancellationToken);
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, cancellationToken);

                var read = await reader.ReadAsync(cancellationToken);
                Debug.Assert(read);

                var existing = reader.GetFieldValue<bool>("existing");
                var updated = reader.GetFieldValue<bool>("updated");

                if (!existing)
                {
                    throw new JobDoesNotExistException(id);
                }

                return updated;
            },
            new WithLoggerState<ImportJobProcessingStatus>(status, _logger),
            cancellationToken);
    }

    private async Task<TResult> WithTransaction<TResult, TState>(
        string id,
        Func<string, NpgsqlConnection, TState, CancellationToken, Task<TResult>> action,
        TState state,
        CancellationToken cancellationToken,
        [CallerMemberName] string? activityName = null)
    {
        using var activity = RegisterTelemetry.StartActivity(
            $"{nameof(PostgresImportJobTracker)}.{activityName}",
            ActivityKind.Internal,
            tags: [
                new("import-job.id", id),
                new("retry.count", 0),
            ]);

        ResilienceContext ctx = ResilienceContextPool.Shared.Get(cancellationToken);
        var success = false;
        try
        {
            ctx.Properties.Set(ActivityPropertyKey, activity);

            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
            var txCtx = new WithTransactionContext<TResult, TState>(id, state, action, conn);

            var result = await _retryPipeline.ExecuteAsync(
                static async (ResilienceContext ctx, WithTransactionContext<TResult, TState> txCtx) =>
                {
                    var cancellationToken = ctx.CancellationToken;
                    var (id, state, action, conn) = txCtx;

                    await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
                    var result = await action(id, conn, state, cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    
                    return result;
                },
                ctx,
                txCtx);

            success = true;
            return result!;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.CheckViolation && e.ConstraintName 
            is "source_max_positive"
            or "enqueued_max_positive"
            or "processed_max_positive"
            or "enqueued_max_less_than_or_equal_to_source_max"
            or "processed_max_less_than_or_equal_to_enqueued_max")
        {
            var msg = e.ConstraintName switch
            {
                "source_max_positive" => "Source max must be positive",
                "enqueued_max_positive" => "Enqueued max must be positive",
                "processed_max_positive" => "Processed max must be positive",
                "enqueued_max_less_than_or_equal_to_source_max" => "Enqueued max must be less than or equal to source max",
                "processed_max_less_than_or_equal_to_enqueued_max" => "Processed max must be less than or equal to enqueued max",
                _ => Unreachable<string>(e),
            };

            throw new InvalidOperationException(msg, e);
        }
        finally
        {
            if (!success)
            {
                activity?.SetTag("error", "true");
            }

            ResilienceContextPool.Shared.Return(ctx);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static T Unreachable<T>(Exception inner) => throw new UnreachableException(null, inner);
    }

    private Task WithTransaction<TState>(
        string id,
        Func<string, NpgsqlConnection, TState, CancellationToken, Task> action,
        TState state,
        CancellationToken cancellationToken,
        [CallerMemberName] string? activityName = null)
    {
        return WithTransaction<object?, WithTransactionObjectStateWrapper<TState>>(
            id,
            static async (id, conn, stateWrapper, cancellationToken) =>
            {
                var (innerAction, state) = stateWrapper;
                await innerAction(id, conn, state, cancellationToken);

                return null;
            },
            new(action, state),
            cancellationToken,
            activityName);
    }

    private readonly record struct WithTransactionContext<TResult, TState>(
        string Id,
        TState State,
        Func<string, NpgsqlConnection, TState, CancellationToken, Task<TResult>> Action,
        NpgsqlConnection Connection);

    private readonly record struct WithTransactionObjectStateWrapper<TState>(
        Func<string, NpgsqlConnection, TState, CancellationToken, Task> InnerAction,
        TState State);

    private readonly record struct WithLoggerState<T>(T State, ILogger Logger);

    private sealed class JobDoesNotExistException(string jobName)
        : InvalidOperationException($"Job '{jobName}' does not exist")
    {
    }

    private static partial class Log
    {
        [LoggerMessage(0, LogLevel.Debug, "Failed to commit transaction due to serialization error")]
        public static partial void TransactionSerializationError(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Updating queue status for job {JobId} with source max {SourceMax} and enqueued max {EnqueuedMax}")]
        private static partial void UpdateQueueStatus(ILogger logger, string jobId, ulong sourceMax, ulong enqueuedMax);

        public static void UpdateQueueStatus(ILogger logger, string jobId, ImportJobQueueStatus status) 
            => UpdateQueueStatus(logger, jobId, status.SourceMax, status.EnqueuedMax);

        [LoggerMessage(2, LogLevel.Debug, "Updating processed status for job {JobId} with processed max {ProcessedMax}")]
        private static partial void UpdateProcessedStatus(ILogger logger, string jobId, ulong processedMax);

        public static void UpdateProcessedStatus(ILogger logger, string jobId, ImportJobProcessingStatus status)
            => UpdateProcessedStatus(logger, jobId, status.ProcessedMax);
    }
}
