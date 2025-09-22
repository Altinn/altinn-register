using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Register.Persistence;

/// <summary>
/// Persistence service for external role definitions.
/// </summary>
internal sealed partial class PostgreSqlExternalRoleDefinitionPersistence
{
    /// <summary>
    /// Cache for external role definitions.
    /// </summary>
    /// <remarks>
    /// Given that adding role definitions requires redeploying the application, we can cache them indefinitely.
    /// If ever the ability to add role definitions dynamically is added, this will need some sort of eviction policy.
    /// </remarks>
    internal sealed partial class Cache
    {
        private readonly Lock _lock = new();
        private readonly NpgsqlDataSource _dataSource;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<Cache> _logger;

        private object? _state; // State | Task<State> | null

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class.
        /// </summary>
        public Cache(
            ILogger<Cache> logger,
            NpgsqlDataSource dataSource,
            IHostApplicationLifetime applicationLifetime)
        {
            _logger = logger;
            _dataSource = dataSource;
            _applicationLifetime = applicationLifetime;
        }

        /// <summary>
        /// Tries to get the role definition for the specified source and identifier.
        /// </summary>
        /// <param name="source">The role definition source.</param>
        /// <param name="identifier">The role definition identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>A <see cref="ExternalRoleDefinition"/>, if found.</returns>
        public ValueTask<ExternalRoleDefinition?> TryGetRoleDefinition(ExternalRoleSource source, string identifier, CancellationToken cancellationToken)
        {
            var key = new RoleKey(source, identifier);

            return WithState(key, static (state, key) => state.TryGetRoleDefinition(key), cancellationToken);
        }

        /// <summary>
        /// Tries to get the role definition for the specified role-code.
        /// </summary>
        /// <param name="roleCode">The role definition role-code.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>A <see cref="ExternalRoleDefinition"/>, if found.</returns>
        public ValueTask<ExternalRoleDefinition?> TryGetRoleDefinitionByRoleCode(string roleCode, CancellationToken cancellationToken)
        {
            return WithState(roleCode, static (state, roleCode) => state.TryGetRoleDefinitionByRoleCode(roleCode), cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<ExternalRoleDefinition?> WithState<TArg>(
            TArg arg,
            Func<State, TArg, ExternalRoleDefinition?> func,
            CancellationToken cancellationToken)
        {
            object? current = Volatile.Read(ref _state);
            if (current is State s)
            {
                return new(func(s, arg));
            }

            return WithStateAsync(arg, func, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ValueTask<T> WithStateAsync<T, TArg>(
            TArg arg,
            Func<State, TArg, T> func,
            CancellationToken cancellationToken)
        {
            var stateTask = GetState(cancellationToken);

            if (!stateTask.IsCompletedSuccessfully)
            {
                return AwaitStateAsync(stateTask, arg, func);
            }

            var state = stateTask.GetAwaiter().GetResult();
            return ValueTask.FromResult(func(state, arg));
            
            async static ValueTask<T> AwaitStateAsync(ValueTask<State> stateTask, TArg arg, Func<State, TArg, T> func)
            {
                var state = await stateTask;

                return func(state, arg);
            }
        }

        private ValueTask<State> GetState(CancellationToken cancellationToken)
        {
            object? current;
            Task<Task<State>>? coldTask = null;
            lock (_lock)
            {
                current = Volatile.Read(ref _state);

                if (current is State s)
                {
                    return ValueTask.FromResult(s);
                }

                if (current is null)
                {
                    coldTask = new Task<Task<State>>(() => LoadStateAsync(_applicationLifetime.ApplicationStopping));
                    current = coldTask.Unwrap().ContinueWith(
                        task =>
                        {
                            if (!task.IsCompletedSuccessfully)
                            {
                                return task;
                            }

                            var result = task.GetAwaiter().GetResult();
                            lock (_lock)
                            {
                                if (ReferenceEquals(_state, current))
                                {
                                    if (task.IsCompletedSuccessfully)
                                    {
                                        _state = result;
                                        return task;
                                    }
                                    else
                                    {
                                        _state = null;
                                    }
                                }
                            }

                            // state has been updated since this task started, use the new value
                            return GetState(cancellationToken).AsTask();
                        },
                        TaskContinuationOptions.ExecuteSynchronously)
                        .Unwrap();

                    Volatile.Write(ref _state, current);
                }
            }

            // we don't want to run any of the LoadStateAsync logic while holding the lock
            coldTask?.Start();

            Debug.Assert(current is Task<State>);
            return new(Unsafe.As<Task<State>>(current));
        }

        private async Task<State> LoadStateAsync(CancellationToken cancellationToken)
        {
            const string QUERY
                = /*strpsql*/"""
                SELECT source, identifier, name, description, code
                FROM register.external_role_definition
                """;

            Log.FetchingExternalRoleDefinitions(_logger);
            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = QUERY;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var byRoleKey = new Dictionary<RoleKey, ExternalRoleDefinition>();
            var byRoleCode = new Dictionary<string, ExternalRoleDefinition>();

            await foreach (var roleDefinition in ReadExternalRoleDefinitions(reader, cancellationToken))
            {
                var key = new RoleKey(roleDefinition.Source, roleDefinition.Identifier);
                byRoleKey.Add(key, roleDefinition);

                if (roleDefinition.Code is not null)
                {
                    byRoleCode.Add(roleDefinition.Code, roleDefinition);
                }
            }

            return new State(byRoleKey, byRoleCode);
        }

        /// <summary>
        /// Reads external role definitions from the specified <see cref="NpgsqlDataReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="NpgsqlDataReader"/>.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>An async enumerable of <see cref="ExternalRoleDefinition"/>s.</returns>
        /// <remarks>Used by tests.</remarks>
        internal static async IAsyncEnumerable<ExternalRoleDefinition> ReadExternalRoleDefinitions(NpgsqlDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var sourceOrdinal = reader.GetOrdinal("source");
            var identifierOrdinal = reader.GetOrdinal("identifier");
            var nameOrdinal = reader.GetOrdinal("name");
            var descriptionOrdinal = reader.GetOrdinal("description");
            var codeOrdinal = reader.GetOrdinal("code");

            while (await reader.ReadAsync(cancellationToken))
            {
                var source = await reader.GetFieldValueAsync<ExternalRoleSource>(sourceOrdinal, cancellationToken);
                var identifier = await reader.GetFieldValueAsync<string>(identifierOrdinal, cancellationToken);
                var name = await reader.GetConvertibleFieldValueAsync<Dictionary<string, string>, TranslatedText>(nameOrdinal, cancellationToken);
                var description = await reader.GetConvertibleFieldValueAsync<Dictionary<string, string>, TranslatedText>(descriptionOrdinal, cancellationToken);
                var code = await reader.GetFieldValueOrDefaultAsync<string>(codeOrdinal, cancellationToken);

                var roleDefinition = new ExternalRoleDefinition()
                {
                    Source = source,
                    Identifier = identifier,
                    Name = name,
                    Description = description,
                    Code = code,
                };

                yield return roleDefinition;
            }
        }

        private readonly record struct RoleKey(ExternalRoleSource Source, string Identifier);

        private sealed class State
        {
            private readonly ImmutableArray<ExternalRoleDefinition> _all;
            private readonly FrozenDictionary<RoleKey, ExternalRoleDefinition> _byRoleKey;
            private readonly FrozenDictionary<string, ExternalRoleDefinition> _byRoleCode;

            public State(
                Dictionary<RoleKey, ExternalRoleDefinition> byRoleKey,
                Dictionary<string, ExternalRoleDefinition> byRoleCode)
            {
                _all = byRoleKey.Values.ToImmutableArray();
                _byRoleKey = byRoleKey.ToFrozenDictionary(byRoleKey.Comparer);
                _byRoleCode = byRoleCode.ToFrozenDictionary(byRoleCode.Comparer);
            }

            public ExternalRoleDefinition? TryGetRoleDefinition(RoleKey key)
                => _byRoleKey.TryGetValue(key, out var value) ? value : null;

            public ExternalRoleDefinition? TryGetRoleDefinitionByRoleCode(string roleCode)
                => _byRoleCode.TryGetValue(roleCode, out var value) ? value : null;
        }

        private static partial class Log
        {
            [LoggerMessage(0, LogLevel.Debug, "Fetching external role definitions.")]
            public static partial void FetchingExternalRoleDefinitions(ILogger logger);
        }
    }
}
