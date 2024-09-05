using System.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Manager for PostgreSql databases used in tests.
/// </summary>
public static class PostgreSqlManager
{
    private static readonly AsyncLazyReferenceCounted<Container> _container
        = AsyncLazyReferenceCounted.Create<Container>(allowReuse: true);

    /// <summary>
    /// Creates a new database.
    /// </summary>
    /// <returns></returns>
    public static async Task<PostgreSqlDatabase> CreateDatabase()
    {
        var dbRef = await _container.Get();

        try
        {
            var serverConnectionString = dbRef.Value.ConnectionString;
            var dbName = dbRef.Value.NextDatabaseName();
            var owner = CreateUser(dbName, "owner");
            var migrator = CreateUser(dbName, "migrator");
            var seeder = CreateUser(dbName, "seeder");
            var app = CreateUser(dbName, "app");

            await using var connection = await dbRef.Value.GetConnection();
            await CreateRoles(connection, owner, migrator, seeder, app);
            await CreateDatabase(connection, dbName, owner.Name);
            await GrantDatabaseAccess(connection, dbName, migrator.Name, seeder.Name, app.Name);

            var ownerConnectionString = ConnectionStringFor(serverConnectionString, dbName, owner);
            var migratorConnectionString = ConnectionStringFor(serverConnectionString, dbName, migrator);
            var seederConnectionString = ConnectionStringFor(serverConnectionString, dbName, seeder);
            var connectionString = ConnectionStringFor(serverConnectionString, dbName, app);

            var db = await Database.Create(dbRef, dbName);
            var result = new PostgreSqlDatabase(db, dbName, ownerConnectionString, migratorConnectionString, seederConnectionString, connectionString);
            dbRef = null;
            return result;
        }
        finally
        {
            if (dbRef is { } db)
            {
                await db.DisposeAsync();
            }
        }

        static async Task GrantDatabaseAccess(
            NpgsqlConnection connection,
            string dbName,
            string migrator,
            string seeder,
            string app)
        {
            await using var batch = connection.CreateBatch();
            AddCommand(batch, /*strpsql*/$"GRANT CREATE, CONNECT ON DATABASE \"{dbName}\" TO \"{migrator}\"");
            AddCommand(batch, /*strpsql*/$"GRANT CONNECT ON DATABASE \"{dbName}\" TO \"{seeder}\"");
            AddCommand(batch, /*strpsql*/$"GRANT CONNECT ON DATABASE \"{dbName}\" TO \"{app}\"");

            await batch.ExecuteNonQueryAsync();
        }

        static async Task CreateDatabase(
            NpgsqlConnection connection,
            string dbName,
            string owner)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = /*strpsql*/$"CREATE DATABASE \"{dbName}\" OWNER \"{owner}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        static async Task CreateRoles(
            NpgsqlConnection connection, 
            (string Name, string Pass) owner, 
            (string Name, string Pass) migrator, 
            (string Name, string Pass) seeder, 
            (string Name, string Pass) app)
        {
            await using var batch = connection.CreateBatch();
            AddCommand(batch, /*strpsql*/$"CREATE ROLE \"{owner.Name}\" WITH LOGIN PASSWORD '{owner.Pass}'");
            AddCommand(batch, /*strpsql*/$"CREATE ROLE \"{migrator.Name}\" WITH LOGIN PASSWORD '{migrator.Pass}'");
            AddCommand(batch, /*strpsql*/$"CREATE ROLE \"{seeder.Name}\" WITH LOGIN PASSWORD '{seeder.Pass}'");
            AddCommand(batch, /*strpsql*/$"CREATE ROLE \"{app.Name}\" WITH LOGIN PASSWORD '{app.Pass}'");
            AddCommand(batch, /*strpsql*/$"GRANT \"{migrator.Name}\" TO \"{owner.Name}\"");
            AddCommand(batch, /*strpsql*/$"GRANT \"{seeder.Name}\" TO \"{owner.Name}\"");
            AddCommand(batch, /*strpsql*/$"GRANT \"{app.Name}\" TO \"{owner.Name}\"");
            AddCommand(batch, /*strpsql*/$"GRANT \"{app.Name}\" TO \"{seeder.Name}\"");
            AddCommand(batch, /*strpsql*/$"GRANT \"{app.Name}\" TO \"{migrator.Name}\"");

            await batch.ExecuteNonQueryAsync();
        }

        static void AddCommand(NpgsqlBatch batch, string commandText)
        {
            var cmd = batch.CreateBatchCommand();
            cmd.CommandText = commandText;
            batch.BatchCommands.Add(cmd);
        }

        static (string Name, string Pass) CreateUser(string dbName, string userName)
        {
            return ($"{dbName}_{userName}", $"{dbName}_{userName}_{Guid.NewGuid():N}");
        }

        static string ConnectionStringFor(string baseConnectionString, string databaseName, (string Name, string Pass) user)
        {
            var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = databaseName,
                Username = user.Name,
                Password = user.Pass,
            };

            return builder.ConnectionString;
        }
    }

    internal sealed class Database
        : IAsyncRef
    {
        private readonly IAsyncRef<Container> _container;
        private readonly string _databaseName;
        private readonly IDisposable _ticket;

        private int _disposed = 0;

        public static async ValueTask<Database> Create(IAsyncRef<Container> container, string databaseName)
            => new Database(container, databaseName, await container.Value.AcquireTicket());

        private Database(IAsyncRef<Container> container, string databaseName, IDisposable ticket)
        {
            _container = container;
            _databaseName = databaseName;
            _ticket = ticket;
        }

        bool IAsyncRef.IsDisposed => Volatile.Read(ref _disposed) == 1;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                {
                    await using var conn = await _container.Value.GetConnection();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = /*strpsql*/$"DROP DATABASE IF EXISTS \"{_databaseName}\"";
                    await cmd.ExecuteNonQueryAsync();
                }

                _ticket.Dispose();
                await _container.DisposeAsync();
            }
        }
    }

    internal sealed class Container
        : IAsyncResource<Container>
    {
        private const int MAX_CONCURRENCY = 20;

        private readonly PostgreSqlContainer _container;
        private readonly NpgsqlDataSource _dataSource;
        private readonly AsyncConcurrencyLimiter _throttler = new(MAX_CONCURRENCY);
        private readonly string _connectionString;

        private int _dbCounter = 0;

        public Container(PostgreSqlContainer container)
        {
            _container = container;
            _connectionString = $"{_container.GetConnectionString()}; Include Error Detail=true; Pooling=false;";
            _dataSource = NpgsqlDataSource.Create(_connectionString);
        }

        public string ConnectionString => _connectionString;

        public ValueTask<NpgsqlConnection> GetConnection()
            => _dataSource.OpenConnectionAsync();

        public string NextDatabaseName()
            => $"db_{Interlocked.Increment(ref _dbCounter):D4}";

        public Task<IDisposable> AcquireTicket()
            => _throttler.Acquire();

        public static async ValueTask<Container> New()
        {
            Console.WriteLine("Creating new PostgreSql container...");

            var username = Debugger.IsAttached ? "username" : Guid.NewGuid().ToString("N");
            var password = Debugger.IsAttached ? "password" : Guid.NewGuid().ToString("N");

            var builder = new PostgreSqlBuilder()
                .WithImage("docker.io/postgres:16.1-alpine")
                .WithUsername(username)
                .WithPassword(password)
                .WithCleanUp(true);

            if (Debugger.IsAttached)
            {
                builder = builder.WithPortBinding(44181, PostgreSqlBuilder.PostgreSqlPort);
            }

            var container = builder.Build();

            try
            {
                await container.StartAsync();
                var result = new Container(container);
                container = null;
                return result;
            }
            finally
            {
                if (container is { } c)
                {
                    await c.DisposeAsync();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _dataSource.DisposeAsync();
            await _container.DisposeAsync();
        }
    }
}
