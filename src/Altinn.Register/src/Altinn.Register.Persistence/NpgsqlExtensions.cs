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

    /// <summary>
    /// Gets a field value or a default value if the field is <see cref="DBNull.Value"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="reader">The DB reader.</param>
    /// <param name="ordinal">The column ordinal.</param>
    /// <param name="defaultValue">Optional default value, defaults to <see langword="default"/>.</param>
    /// <returns>The column value, or <paramref name="defaultValue"/> if the field is <see cref="DBNull.Value"/>.</returns>
    public static T? GetFieldValueOrDefault<T>(this NpgsqlDataReader reader, int ordinal, T? defaultValue = default)
        where T : notnull
        => reader.IsDBNull(ordinal) ? defaultValue : reader.GetFieldValue<T>(ordinal);

    /// <summary>
    /// Gets a field value or a default value if the field is <see cref="DBNull.Value"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="reader">The DB reader.</param>
    /// <param name="name">The column name.</param>
    /// <param name="defaultValue">Optional default value, defaults to <see langword="default"/>.</param>
    /// <returns>The column value, or <paramref name="defaultValue"/> if the field is <see cref="DBNull.Value"/>.</returns>
    public static T? GetFieldValueOrDefault<T>(this NpgsqlDataReader reader, string name, T? defaultValue = default)
        where T : notnull
        => GetFieldValueOrDefault(reader, reader.GetOrdinal(name), defaultValue);
}
