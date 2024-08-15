using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Swashbuckle.Examples;
using Altinn.Swashbuckle.Filters;

namespace Altinn.Register.Core.Parties;

/// <summary>
/// A organization number (a string of 9 digits).
/// </summary>
[SwaggerString(Format = "ssn", Pattern = "^[0-9]{11}$")]
[JsonConverter(typeof(PersonIdentifier.JsonConverter))]
public class PersonIdentifier
    : IParsable<PersonIdentifier>
    , ISpanParsable<PersonIdentifier>
    , IFormattable
    , ISpanFormattable
    , IExampleDataProvider<PersonIdentifier>
    , IEquatable<PersonIdentifier>
    , IEquatable<string>
    , IEqualityOperators<PersonIdentifier, PersonIdentifier, bool>
    , IEqualityOperators<PersonIdentifier, string, bool>
{
    private const int LENGTH = 11;
    private static readonly SearchValues<char> NUMBERS = SearchValues.Create(['0', '1', '2', '3', '4', '5', '6', '7', '8', '9']);

    private readonly string _value;

    private PersonIdentifier(string value)
    {
        _value = value;
    }

    /// <inheritdoc/>
    public static IEnumerable<PersonIdentifier>? GetExamples(ExampleDataOptions options)
    {
        yield return new PersonIdentifier("12345678910");
        yield return new PersonIdentifier("98765432101");
    }

    /// <inheritdoc cref="IParsable{TSelf}.Parse(string, IFormatProvider?)"/>
    public static PersonIdentifier Parse(string s)
        => Parse(s, provider: null);

    /// <inheritdoc/>
    public static PersonIdentifier Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result)
        ? result
        : throw new FormatException("Invalid SSN");

    /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)"/>
    public static PersonIdentifier Parse(ReadOnlySpan<char> s)
        => Parse(s, provider: null);

    /// <inheritdoc/>
    public static PersonIdentifier Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result)
        ? result
        : throw new FormatException("Invalid SSN");

    /// <inheritdoc/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out PersonIdentifier result)
        => TryParse(s.AsSpan(), s, out result);

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out PersonIdentifier result)
        => TryParse(s, original: null, out result);

    private static bool TryParse(ReadOnlySpan<char> s, string? original, [MaybeNullWhen(false)] out PersonIdentifier result)
    {
        if (s.Length != LENGTH)
        {
            result = null;
            return false;
        }

        if (s.ContainsAnyExcept(NUMBERS))
        {
            result = null;
            return false;
        }

        result = new PersonIdentifier(original ?? new string(s));
        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
        => _value;

    /// <inheritdoc cref="IFormattable.ToString(string?, IFormatProvider?)"/>
    public string ToString(string? format)
        => _value;

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => _value;

    /// <inheritdoc/>
    public bool Equals(PersonIdentifier? other)
        => ReferenceEquals(this, other) || (other is not null && _value == other._value);

    /// <inheritdoc/>
    public bool Equals(string? other)
        => other is not null && _value == other;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj switch
        {
            PersonIdentifier other => Equals(other),
            string other => Equals(other),
            _ => false,
        };

    /// <inheritdoc/>
    public override int GetHashCode()
        => _value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (destination.Length < _value.Length)
        {
            charsWritten = 0;
            return false;
        }

        _value.AsSpan().CopyTo(destination);
        charsWritten = _value.Length;
        return true;
    }

    /// <inheritdoc/>
    public static bool operator ==(PersonIdentifier? left, PersonIdentifier? right)
        => ReferenceEquals(left, right) || (left?.Equals(right) ?? right is null);

    /// <inheritdoc/>
    public static bool operator !=(PersonIdentifier? left, PersonIdentifier? right)
        => !(left == right);

    /// <inheritdoc/>
    public static bool operator ==(PersonIdentifier? left, string? right)
        => left?.Equals(right) ?? right is null;

    /// <inheritdoc/>
    public static bool operator !=(PersonIdentifier? left, string? right)
        => !(left == right);

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality"/>
    public static bool operator ==(string? left, PersonIdentifier? right)
        => right?.Equals(left) ?? left is null;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality"/>
    public static bool operator !=(string? left, PersonIdentifier? right)
        => !(left == right);

    private sealed class JsonConverter : JsonConverter<PersonIdentifier>
    {
        public override PersonIdentifier? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (!TryParse(str, null, out var result))
            {
                throw new JsonException("Invalid SSN");
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, PersonIdentifier value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value._value);
        }
    }
}
