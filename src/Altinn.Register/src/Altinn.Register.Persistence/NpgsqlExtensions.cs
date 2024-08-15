using Npgsql;

namespace Altinn.Register.Persistence;

/// <summary>
/// Extension methods for Npgsql.
/// </summary>
internal static class NpgsqlExtensions
{
    /// <summary>
    /// Adds a typed parameter to the collection.
    /// </summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <param name="collection">The parameter collection.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The newly created parameter.</returns>
    public static NpgsqlParameter<T> Add<T>(this NpgsqlParameterCollection collection, string parameterName)
    {
        var parameter = new NpgsqlParameter<T>()
        {
            ParameterName = parameterName,
        };

        collection.Add(parameter);
        return parameter;
    }
}
