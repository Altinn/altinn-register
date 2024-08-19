using Altinn.Register.Core.Parties;
using Npgsql;

namespace Altinn.Register.Persistence;

/// <summary>
/// Implementation of <see cref="IV1PartyService"/> backed by a PostgreSQL database.
/// </summary>
internal class PostgreSqlPartyPersistence
    : IPartyPersistence
{
    private readonly NpgsqlConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlPartyPersistence"/> class.
    /// </summary>
    public PostgreSqlPartyPersistence(NpgsqlConnection connection)
    {
        _connection = connection;
    }
}
