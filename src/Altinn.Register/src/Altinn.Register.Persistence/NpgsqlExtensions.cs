using Altinn.Register.Core.Utils;
using CommunityToolkit.Diagnostics;
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
        => GetFieldValueOrDefault(reader, reader.GetOrdinal(name), defaultValue);

    /// <summary>
    /// Gets a conditional field value as a <see cref="FieldValue{T}"/>.
    /// </summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="reader">The <see cref="NpgsqlDataReader"/>.</param>
    /// <param name="ordinal">The column ordinal.</param>
    /// <returns>A <see cref="FieldValue{T}"/>.</returns>
    public static FieldValue<T> GetConditionalFieldValue<T>(this NpgsqlDataReader reader, int ordinal)
        where T : notnull
    {
        if (ordinal == -1)
        {
            return FieldValue<T>.Unset;
        }

        if (reader.IsDBNull(ordinal))
        {
            return FieldValue<T>.Null;
        }

        return (FieldValue<T>)reader.GetFieldValue<T>(ordinal);
    }

    /// <summary>
    /// Gets a conditional field value as a <see cref="FieldValue{T}"/>.
    /// </summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="reader">The <see cref="NpgsqlDataReader"/>.</param>
    /// <param name="name">The column name.</param>
    /// <returns>A <see cref="FieldValue{T}"/>.</returns>
    public static FieldValue<T> GetConditionalFieldValue<T>(this NpgsqlDataReader reader, string name)
        where T : notnull
        => GetConditionalFieldValue<T>(reader, reader.GetOrdinal(name));

    /// <summary>
    /// Gets a conditional field value as a <see cref="FieldValue{T}"/>, parsed from a string using <see cref="IParsable{TSelf}"/>.
    /// </summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="reader">The <see cref="NpgsqlDataReader"/>.</param>
    /// <param name="ordinal">The column ordinal.</param>
    /// <returns>A <see cref="FieldValue{T}"/>.</returns>
    /// <exception cref="FormatException">The database value failed to parse as a <typeparamref name="T"/>.</exception>
    public static FieldValue<T> GetConditionalParsableFieldValue<T>(this NpgsqlDataReader reader, int ordinal)
        where T : notnull, IParsable<T>
    {
        if (ordinal == -1)
        {
            return FieldValue<T>.Unset;
        }

        if (reader.IsDBNull(ordinal))
        {
            return FieldValue<T>.Null;
        }

        var value = reader.GetString(ordinal);

        if (!T.TryParse(value, provider: null, out var result))
        {
            return ThrowParseError<T>(reader, ordinal);
        }

        return result;
    }

    /// <summary>
    /// Gets a conditional field value as a <see cref="FieldValue{T}"/>, parsed from a string using <see cref="IParsable{TSelf}"/>.
    /// </summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="reader">The <see cref="NpgsqlDataReader"/>.</param>
    /// <param name="name">The column name.</param>
    /// <returns>A <see cref="FieldValue{T}"/>.</returns>
    /// <exception cref="FormatException">The database value failed to parse as a <typeparamref name="T"/>.</exception>
    public static FieldValue<T> GetConditionalParsableFieldValue<T>(this NpgsqlDataReader reader, string name)
        where T : notnull, IParsable<T>
        => GetConditionalParsableFieldValue<T>(reader, reader.GetOrdinal(name));

    private static FieldValue<T> ThrowParseError<T>(NpgsqlDataReader reader, int ordinal)
        where T : notnull
    {
        var columnName = reader.GetName(ordinal);

        if (string.IsNullOrEmpty(columnName))
        {
            columnName = $"column {ordinal}";
        }

        return ThrowHelper.ThrowFormatException<FieldValue<T>>($"Failed to parse value of {columnName} as {typeof(T).Name}");
    }
}
