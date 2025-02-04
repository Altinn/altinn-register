using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Hosting;
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
    internal sealed class Cache
    {
        private readonly Lock _lock = new();
        private readonly NpgsqlDataSource _dataSource;
        private readonly TimeProvider _timeProvider;
        private readonly IHostApplicationLifetime _applicationLifetime;

        private object? _state; // State | Task<State> | null

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class.
        /// </summary>
        public Cache(
            NpgsqlDataSource dataSource,
            TimeProvider timeProvider,
            IHostApplicationLifetime applicationLifetime)
        {
            _dataSource = dataSource;
            _timeProvider = timeProvider;
            _applicationLifetime = applicationLifetime;
        }

        /// <summary>
        /// Tries to get the role definition for the specified source and identifier.
        /// </summary>
        /// <param name="source">The role definition source.</param>
        /// <param name="identifier">The role definition identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>A <see cref="ExternalRoleDefinition"/>, if found.</returns>
        public ValueTask<ExternalRoleDefinition?> TryGetRoleDefinition(PartySource source, string identifier, CancellationToken cancellationToken)
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
        private ValueTask<ExternalRoleDefinition?> WithStateAsync<TArg>(
            TArg arg,
            Func<State, TArg, ExternalRoleDefinition?> func,
            CancellationToken cancellationToken)
        {
            object? current;
            Task<Task<State>>? coldTask = null;
            lock (_lock)
            {
                current = Volatile.Read(ref _state);
                
                if (current is State s)
                {
                    return new(func(s, arg));
                }

                if (current is null)
                {
                    coldTask = new Task<Task<State>>(() => LoadStateAsync(_applicationLifetime.ApplicationStopping));
                    current = coldTask.Unwrap().ContinueWith(
                        task =>
                        {
                            lock (_lock)
                            {
                                if (ReferenceEquals(_state, current))
                                {
                                    if (task.IsCompletedSuccessfully)
                                    {
                                        _state = task.Result;
                                    }
                                    else
                                    {
                                        _state = null;
                                    }
                                }

                                return task.Result;
                            }
                        },
                        TaskContinuationOptions.ExecuteSynchronously);
                }
            }

            // we don't want to run any of the LoadStateAsync logic while holding the lock
            coldTask?.Start();

            Debug.Assert(current is Task<State>);
            return new(AwaitStateAsync(Unsafe.As<Task<State>>(current), arg, func, cancellationToken));
        }

        private async Task<ExternalRoleDefinition?> AwaitStateAsync<TArg>(
            Task<State> stateTask,
            TArg arg,
            Func<State, TArg, ExternalRoleDefinition?> func,
            CancellationToken cancellationToken)
        {
            var state = await stateTask.WaitAsync(cancellationToken);
            return func(state, arg);
        }

        private async Task<State> LoadStateAsync(CancellationToken cancellationToken)
        {
            const string QUERY
                = /*strpsql*/"""
                SELECT source, identifier, name, description, code
                FROM register.external_role_definition
                """;

            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = QUERY;

            await cmd.PrepareAsync(cancellationToken);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var byRoleKey = new Dictionary<RoleKey, ExternalRoleDefinition>();
            var byRoleCode = new Dictionary<string, ExternalRoleDefinition>();

            var sourceOrdinal = reader.GetOrdinal("source");
            var identifierOrdinal = reader.GetOrdinal("identifier");
            var nameOrdinal = reader.GetOrdinal("name");
            var descriptionOrdinal = reader.GetOrdinal("description");
            var codeOrdinal = reader.GetOrdinal("code");

            while (await reader.ReadAsync(cancellationToken))
            {
                var source = await reader.GetFieldValueAsync<PartySource>(sourceOrdinal, cancellationToken);
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

                var key = new RoleKey(source, identifier);
                byRoleKey.Add(key, roleDefinition);

                if (code is not null)
                {
                    byRoleCode.Add(code, roleDefinition);
                }
            }

            return new State(byRoleKey, byRoleCode);
        }

        private readonly record struct RoleKey(PartySource Source, string Identifier);

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
    }
}
