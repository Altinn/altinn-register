using System.Diagnostics;
using Altinn.Register.TestUtils.Async;
using Altinn.Register.TestUtils.Tracing;
using CommunityToolkit.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Altinn.Register.TestUtils.Database;

public class PostgresServerFixture
    : IAsyncLifetime
{
    private const int MAX_CONCURRENCY = 2;

    private readonly AsyncConcurrencyLimiter _throttler = new(MAX_CONCURRENCY);
    private readonly PostgreSqlContainer _container;

    private NpgsqlDataSource? _dataSource;
    private int _dbCounter = 0;

    public PostgresServerFixture()
    {
        var username = Debugger.IsAttached ? "username" : Guid.NewGuid().ToString("N");
        var password = Debugger.IsAttached ? "password" : Guid.NewGuid().ToString("N");

        var builder = new PostgreSqlBuilder()
            .WithImage("docker.io/postgres:16.2-alpine")
            .WithUsername(username)
            .WithPassword(password)
            .WithCommand("-c", "max_locks_per_transaction=4096")
            .WithCleanUp(true);

        if (Debugger.IsAttached)
        {
            builder = builder.WithPortBinding(44181, PostgreSqlBuilder.PostgreSqlPort);
        }

        _container = builder.Build();
    }

    public async Task<PostgresDatabase> CreateDatabase(CancellationToken cancellationToken)
    {
        var dbName = $"db_{Interlocked.Increment(ref _dbCounter):D4}";
        using var rootActivity = TestUtilsActivities.Source.StartActivity(ActivityKind.Internal, name: $"create database {dbName}");

        var owner = DefineUser(dbName, PostgresUserType.Owner);
        var migrator = DefineUser(dbName, PostgresUserType.Migrator);
        var seeder = DefineUser(dbName, PostgresUserType.Seeder);
        var app = DefineUser(dbName, PostgresUserType.App);

        IDisposable? ticket = null;
        DatabaseHandle? handle = null;
        try
        {
            {
                using var waitActivity = TestUtilsActivities.Source.StartActivity(ActivityKind.Internal, name: "acquire database ticket")
                    .HideIfShorterThan(TimeSpan.FromMilliseconds(50));

                ticket = await _throttler.Acquire();
            }

            handle = new DatabaseHandle(this, ticket, dbName, owner: owner, migrator: migrator, seeder: seeder, app: app);
            ticket = null;

            {
                await using var conn = await _dataSource!.OpenConnectionAsync(cancellationToken);
                await CreateRoles(conn, owner: owner, migrator: migrator, seeder: seeder, app: app, cancellationToken);
                await CreateDatabase(conn, dbName, owner, cancellationToken);
                await GrantPrivileges(conn, dbName, migrator: migrator, seeder: seeder, app: app, cancellationToken);
            }

            var result = new PostgresDatabase(handle);
            handle = null;
            return result;
        }
        finally
        {
            if (handle is { } h)
            {
                await h.DisposeAsync();
            }

            if (ticket is { } t)
            {
                t.Dispose();
            }
        }

        static DbUser DefineUser(string dbName, PostgresUserType userType)
        {
            var typeName = UserTypeString(userType);
            return new($"{dbName}_{typeName}", $"{dbName}_{typeName}_{Guid.NewGuid():N}", userType);
        }

        static async Task CreateRoles(NpgsqlConnection conn, DbUser owner, DbUser migrator, DbUser seeder, DbUser app, CancellationToken cancellationToken)
        {
            using var activity = TestUtilsActivities.Source.StartActivity(ActivityKind.Internal, name: $"create roles");
            await using var batch = conn.CreateBatch();

            AddCommand(
                batch,
                /*strpsql*/$"""
                CREATE ROLE "{owner.Name}" WITH LOGIN PASSWORD '{owner.Pass}'
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                CREATE ROLE "{migrator.Name}" WITH LOGIN PASSWORD '{migrator.Pass}'
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                CREATE ROLE "{seeder.Name}" WITH LOGIN PASSWORD '{seeder.Pass}'
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                CREATE ROLE "{app.Name}" WITH LOGIN PASSWORD '{app.Pass}'
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT "{migrator.Name}" TO "{owner.Name}"
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT "{seeder.Name}" TO "{owner.Name}"
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT "{app.Name}" TO "{owner.Name}"
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT "{app.Name}" TO "{seeder.Name}"
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT "{app.Name}" TO "{migrator.Name}"
                """);

            await batch.ExecuteNonQueryAsync(cancellationToken);
        }

        static async Task CreateDatabase(NpgsqlConnection conn, string dbName, DbUser owner, CancellationToken cancellationToken)
        {
            using var activity = TestUtilsActivities.Source.StartActivity(ActivityKind.Internal, name: $"create database");
            await using var cmd = conn.CreateCommand();

            cmd.CommandText =
                /*strpsql*/$"""
                CREATE DATABASE "{dbName}" OWNER "{owner.Name}"
                """;

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        static async Task GrantPrivileges(NpgsqlConnection conn, string dbName, DbUser migrator, DbUser seeder, DbUser app, CancellationToken cancellationToken)
        {
            using var activity = TestUtilsActivities.Source.StartActivity(ActivityKind.Internal, name: $"grant privileges");
            await using var batch = conn.CreateBatch();

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT CREATE, CONNECT ON DATABASE "{dbName}" TO "{migrator.Name}"
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT CONNECT ON DATABASE "{dbName}" TO "{seeder.Name}"
                """);

            AddCommand(
                batch,
                /*strpsql*/$"""
                GRANT CONNECT ON DATABASE "{dbName}" TO "{app.Name}"
                """);

            await batch.ExecuteNonQueryAsync(cancellationToken);
        }

        static void AddCommand(NpgsqlBatch batch, string commandText)
        {
            var cmd = batch.CreateBatchCommand();
            cmd.CommandText = commandText;
            batch.BatchCommands.Add(cmd);
        }
    }

    private async Task DeleteDatabase(string databaseName)
    {
        using var rootActivity = TestUtilsActivities.Source.StartActivity(ActivityKind.Internal, name: $"delete database {databaseName}");

        await using var conn = await _dataSource!.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/$"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    private static string UserTypeString(PostgresUserType type)
        => type switch
        {
            PostgresUserType.Owner => "owner",
            PostgresUserType.Migrator => "migrator",
            PostgresUserType.Seeder => "seeder",
            PostgresUserType.App => "app",
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<string>(nameof(type)),
        };

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_dataSource is { } dataSource)
        {
            await dataSource.DisposeAsync();
        }

        if (_container is { } container)
        {
            TestContext.Current.TestOutputHelper?.WriteLine("Disposing PostgreSQL container...");

            await container.DisposeAsync();
        }
    }

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        TestContext.Current.TestOutputHelper?.WriteLine("Starting PostgreSQL container...");

        await _container.StartAsync(TestContext.Current.CancellationToken);

        var csBuilder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString());
        csBuilder.IncludeErrorDetail = true;
        csBuilder.Pooling = false;

        _dataSource = NpgsqlDataSource.Create(csBuilder);
    }

    internal record struct DbUser(string Name, string Pass, PostgresUserType Type);

    internal sealed class DatabaseHandle
        : IAsyncDisposable
    {
        private readonly PostgresServerFixture _server;
        private readonly IDisposable _ticket;
        private readonly string _databaseName;
        private readonly DbUser _owner;
        private readonly DbUser _migrator;
        private readonly DbUser _seeder;
        private readonly DbUser _app;

        private int _disposed = 0;
        private string? _ownerConnectionString;
        private string? _migratorConnectionString;
        private string? _seederConnectionString;
        private string? _appConnectionString;

        public DatabaseHandle(
            PostgresServerFixture server,
            IDisposable ticket,
            string databaseName,
            DbUser owner,
            DbUser migrator,
            DbUser seeder,
            DbUser app)
        {
            _server = server;
            _ticket = ticket;
            _databaseName = databaseName;
            _owner = owner;
            _migrator = migrator;
            _seeder = seeder;
            _app = app;
        }

        public string ConnectionString(PostgresUserType type)
        {
            ThrowIfDisposed();

            return type switch
            {
                PostgresUserType.Owner => ConnectionString(in _owner, ref _ownerConnectionString),
                PostgresUserType.Migrator => ConnectionString(in _migrator, ref _migratorConnectionString),
                PostgresUserType.Seeder => ConnectionString(in _seeder, ref _seederConnectionString),
                PostgresUserType.App => ConnectionString(in _app, ref _appConnectionString),
                _ => ThrowHelper.ThrowArgumentOutOfRangeException<string>(nameof(type)),
            };
        }

        private string ConnectionString(in DbUser user, ref string? connectionString)
        {
            if (connectionString is not null)
            {
                return connectionString;
            }

            var builder = new NpgsqlConnectionStringBuilder(_server._dataSource!.ConnectionString) 
            {
                Database = _databaseName,
                Username = user.Name,
                Password = user.Pass,
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 4,
                ConnectionIdleLifetime = 5,
                ConnectionPruningInterval = 5,
                ConnectionLifetime = 30,
            };

            connectionString = builder.ConnectionString;
            return connectionString;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                return DisposeCore(_server, _databaseName, _ticket);
            }

            return ValueTask.CompletedTask;

            async static ValueTask DisposeCore(PostgresServerFixture server, string databaseName, IDisposable ticket)
            {
                await server.DeleteDatabase(databaseName);
                ticket.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(DatabaseHandle));
            }
        }
    }
}
