using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Altinn.Swashbuckle.Filters;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Contracts;

/// <summary>
/// Represents a language code.
/// </summary>
[DebuggerDisplay("{Code}")]
[JsonConverter(typeof(LangCode.JsonConverter))]
[SwaggerString]
public sealed class LangCode
    : IEquatable<LangCode>
    , IComparable<LangCode>
    , IEqualityOperators<LangCode, LangCode, bool>
    , IComparisonOperators<LangCode, LangCode, bool>
{
    /// <summary>
    /// The language code for English.
    /// </summary>
    internal const string EN_CODE = "en";

    /// <summary>
    /// The language code for Norwegian Bokmål.
    /// </summary>
    internal const string NB_CODE = "nb";

    /// <summary>
    /// The language code for Norwegian Nynorsk.
    /// </summary>
    internal const string NN_CODE = "nn";

    /// <summary>
    /// Gets the language code for English.
    /// </summary>
    public static readonly LangCode En = new LangCode(EN_CODE);

    /// <summary>
    /// Gets the language code for Norwegian Bokmål.
    /// </summary>
    public static readonly LangCode Nb = new LangCode(NB_CODE);

    /// <summary>
    /// Gets the language code for Norwegian Nynorsk.
    /// </summary>
    public static readonly LangCode Nn = new LangCode(NN_CODE);

    private static readonly ConcurrentDictionary<string, LangCode> _codes
        = new ConcurrentDictionary<string, LangCode>(
            comparer: StringComparer.OrdinalIgnoreCase,
            collection: [
                new(En.Code, En),
                new(Nb.Code, Nb),
                new(Nn.Code, Nn),
            ]);

    /// <summary>
    /// Gets a language code from a string.
    /// </summary>
    /// <param name="code">The language code as a string.</param>
    /// <returns>A <see cref="LangCode"/>.</returns>
    public static LangCode FromCode(string code)
    {
        return code switch
        {
            // normal cases
            "en" => En,
            "nb" => Nb,
            "nn" => Nn,

            // rest
            _ => GetOrCreateCode(code),
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static LangCode GetOrCreateCode(string code)
        {
            code = code.ToLowerInvariant().Normalize();

            return _codes.GetOrAdd(code, static c => new(c));
        }
    }

    /// <summary>
    /// Gets a language code from a UTF-8 encoded string.
    /// </summary>
    /// <param name="utf8Bytes">The UTF-8 bytes.</param>
    /// <returns>A <see cref="LangCode"/>.</returns>
    public static LangCode FromCode(ReadOnlySpan<byte> utf8Bytes)
    {
        if (utf8Bytes.Length == 2)
        {
            if (utf8Bytes.SequenceEqual("en"u8))
            {
                return En;
            }

            if (utf8Bytes.SequenceEqual("nb"u8))
            {
                return Nb;
            }

            if (utf8Bytes.SequenceEqual("nn"u8))
            {
                return Nn;
            }
        }

        var str = Encoding.UTF8.GetString(utf8Bytes);
        return FromCode(str);
    }

    private readonly string _code;

    /// <summary>
    /// Gets the language code as a string.
    /// </summary>
    public string Code => _code;

    private LangCode(string code)
    {
        Guard.IsNotNull(code);

        _code = code;
    }

    /// <inheritdoc/>
    public override string ToString()
        => _code;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // this does not need to be OrdinalIgnoreCase, as we normalize the code before calling the constructor
        return string.GetHashCode(_code, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return ReferenceEquals(this, obj);
    }

    /// <inheritdoc/>
    public bool Equals(LangCode? other)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return ReferenceEquals(this, other);
    }

    /// <inheritdoc/>
    public int CompareTo(LangCode? other)
    {
        return ReferenceEquals(this, other) ? 0
            : string.Compare(_code, other?._code, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public static bool operator ==(LangCode? left, LangCode? right)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return ReferenceEquals(left, right);
    }

    /// <inheritdoc/>
    public static bool operator !=(LangCode? left, LangCode? right)
    {
        // no two instances of LangCode should have the same code, the global dictionary ensures this
        return !ReferenceEquals(left, right);
    }

    /// <inheritdoc/>
    public static bool operator >(LangCode left, LangCode right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc/>
    public static bool operator >=(LangCode left, LangCode right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc/>
    public static bool operator <(LangCode left, LangCode right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc/>
    public static bool operator <=(LangCode left, LangCode right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// <see cref="System.Text.Json.Serialization.JsonConverter"/> for <see cref="LangCode"/>.
    /// </summary>
    internal sealed class JsonConverter
        : JsonConverter<LangCode>
    {
        /// <inheritdoc cref="JsonConverter{T}.Read(ref Utf8JsonReader, Type, JsonSerializerOptions)"/>
        public static LangCode? Read(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType is not JsonTokenType.String)
            {
                throw new JsonException($"Expected a string, but got {reader.TokenType}.");
            }

            if (reader.ValueIsEscaped || reader.HasValueSequence)
            {
                var str = reader.GetString();
                if (str is null)
                {
                    return null;
                }

                return FromCode(str);
            }

            return FromCode(reader.ValueSpan);
        }

        /// <inheritdoc cref="JsonConverter{T}.ReadAsPropertyName(ref Utf8JsonReader, Type, JsonSerializerOptions)"/>
        public static LangCode ReadAsPropertyName(ref Utf8JsonReader reader)
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected a string, but got {reader.TokenType}.");
            }

            if (reader.ValueIsEscaped || reader.HasValueSequence)
            {
                var str = reader.GetString();
                if (str is null)
                {
                    throw new JsonException($"Expected a property name, but read null");
                }

                return FromCode(str);
            }

            return FromCode(reader.ValueSpan);
        }

        /// <inheritdoc cref="JsonConverter{T}.Write(Utf8JsonWriter, T, JsonSerializerOptions)"/>
        public static void Write(Utf8JsonWriter writer, LangCode value)
        {
            switch (value.Code)
            {
                case EN_CODE:
                    writer.WriteStringValue("en"u8);
                    break;

                case NB_CODE:
                    writer.WriteStringValue("nb"u8);
                    break;

                case NN_CODE:
                    writer.WriteStringValue("nn"u8);
                    break;

                default:
                    writer.WriteStringValue(value.Code);
                    break;
            }
        }

        /// <inheritdoc cref="JsonConverter{T}.WriteAsPropertyName(Utf8JsonWriter, T, JsonSerializerOptions)"/>
        public static void WriteAsPropertyName(Utf8JsonWriter writer, LangCode value)
        {
            switch (value.Code)
            {
                case EN_CODE:
                    writer.WritePropertyName("en"u8);
                    break;

                case NB_CODE:
                    writer.WritePropertyName("nb"u8);
                    break;

                case NN_CODE:
                    writer.WritePropertyName("nn"u8);
                    break;

                default:
                    writer.WritePropertyName(value.Code);
                    break;
            }
        }

        /// <inheritdoc/>
        public override LangCode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Read(ref reader);

        /// <inheritdoc/>
        public override LangCode ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ReadAsPropertyName(ref reader);

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, LangCode value, JsonSerializerOptions options)
            => Write(writer, value);

        /// <inheritdoc/>
        public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] LangCode value, JsonSerializerOptions options)
            => WriteAsPropertyName(writer, value);
    }
}
