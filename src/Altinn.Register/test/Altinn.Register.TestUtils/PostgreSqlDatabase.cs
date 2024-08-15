using Npgsql;

namespace Altinn.Register.TestUtils;

/// <summary>
/// A handle to a PostgreSql database.
/// </summary>
public sealed class PostgreSqlDatabase
    : IAsyncDisposable
{
    private readonly IAsyncRef _containerRef;
    private readonly string _databaseName;
    private readonly string _ownerConnectionString;
    private readonly string _migratorConnectionString;
    private readonly string _seederConnectionString;
    private readonly string _connectionString;

    private NpgsqlDataSource? _ownerDataSource;
    private NpgsqlDataSource? _migratorDataSource;
    private NpgsqlDataSource? _seederDataSource;
    private NpgsqlDataSource? _dataSource;

    internal PostgreSqlDatabase(
        IAsyncRef containerRef,
        string databaseName,
        string ownerConnectionString,
        string migratorConnectionString,
        string seederConnectionString,
        string connectionString)
    {
        _containerRef = containerRef;
        _databaseName = databaseName;
        _ownerConnectionString = ownerConnectionString;
        _migratorConnectionString = migratorConnectionString;
        _seederConnectionString = seederConnectionString;
        _connectionString = connectionString;
    }

    /// <summary>
    /// Gets the owner connection string.
    /// </summary>
    public string OwnerConnectionString
        => _ownerConnectionString;

    /// <summary>
    /// Gets the migrator connection string.
    /// </summary>
    public string MigratorConnectionString
        => _migratorConnectionString;

    /// <summary>
    /// Gets the seeder connection string.
    /// </summary>
    public string SeederConnectionString
        => _seederConnectionString;

    /// <summary>
    /// Gets the connection string.
    /// </summary>
    public string ConnectionString
        => _connectionString;

    /// <summary>
    /// Gets the data source for the owner of the database.
    /// </summary>
    public NpgsqlDataSource OwnerDataSource 
        => GetDataSource(ref _ownerDataSource, _ownerConnectionString);

    /// <summary>
    /// Gets the data source for the migrator of the database.
    /// </summary>
    public NpgsqlDataSource MigratorDataSource 
        => GetDataSource(ref _migratorDataSource, _migratorConnectionString);

    /// <summary>
    /// Gets the data source for the seeder of the database.
    /// </summary>
    public NpgsqlDataSource SeederDataSource 
        => GetDataSource(ref _seederDataSource, _seederConnectionString);

    /// <summary>
    /// Gets the data source for the database.
    /// </summary>
    public NpgsqlDataSource DataSource 
        => GetDataSource(ref _dataSource, _connectionString);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await MaybeDispose(_ownerDataSource);
        await MaybeDispose(_migratorDataSource);
        await MaybeDispose(_seederDataSource);
        await MaybeDispose(_dataSource);
        await _containerRef.DisposeAsync();

        static ValueTask MaybeDispose(NpgsqlDataSource? dataSource)
        {
            return dataSource?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }

    private NpgsqlDataSource GetDataSource(ref NpgsqlDataSource? dataSource, string connectionString)
    {
        _containerRef.ThrowIfDisposed();

        return dataSource ??= NpgsqlDataSource.Create(connectionString);
    }
}
