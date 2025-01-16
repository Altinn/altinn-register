using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Utils;

/// <summary>
/// Static utility class for <see cref="FieldValue{T}"/>.
/// </summary>
public static class FieldValue
{
    /// <summary>
    /// Gets a value that represents an unset field value.
    /// </summary>
    public static readonly UnsetSentinel Unset = default;

    /// <summary>
    /// Gets a value that represents a null field value.
    /// </summary>
    public static readonly NullSentinel Null = default;

    /// <summary>
    /// Creates a <see cref="FieldValue{T}"/> from a nullable struct.
    /// </summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A <see cref="FieldValue{T}"/>.</returns>
    public static FieldValue<T> From<T>(T? value)
        where T : struct
        => value.HasValue ? value.Value : Null;

    /// <summary>
    /// Maps a <see cref="FieldValue{T}"/> to another type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The field value to map.</param>
    /// <param name="selector">The mapper.</param>
    /// <returns>The mapped field value.</returns>
    public static FieldValue<TResult> Select<TSource, TResult>(this FieldValue<TSource> value, Func<TSource, TResult> selector)
        where TSource : notnull
        where TResult : notnull
        => value switch
        {
            { HasValue: true } => selector(value.Value!),
            { IsNull: true } => Null,
            _ => Unset,
        };

    /// <summary>
    /// A value that implicitly converts to any <see cref="FieldValue{T}"/> in the unset state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct UnsetSentinel
    { 
    }

    /// <summary>
    /// A value that implicitly converts to any <see cref="FieldValue{T}"/> in the null state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct NullSentinel
    {
    }

    /// <summary>
    /// Adds support for <see cref="FieldValue{T}"/> to the specified <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="options">The <see cref="JsonSerializerOptions"/>.</param>
    /// <returns></returns>
    public static JsonSerializerOptions WithFieldValueSupport(this JsonSerializerOptions options)
    {
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithFieldValueSupport();
        return options;
    }

    /// <summary>
    /// Adds support for <see cref="FieldValue{T}"/> to the specified <see cref="JsonTypeInfoResolver"/>.
    /// </summary>
    /// <param name="resolver">The <see cref="IJsonTypeInfoResolver"/>.</param>
    /// <returns>A chained <see cref="IJsonTypeInfoResolver"/>.</returns>
    public static IJsonTypeInfoResolver WithFieldValueSupport(this IJsonTypeInfoResolver resolver)
    {
        return resolver.WithAddedModifier(ModifyFieldValueProperties);
    }

    private static void ModifyFieldValueProperties(JsonTypeInfo jsonTypeInfo)
    {
        foreach (var property in jsonTypeInfo.Properties)
        {
            var propertyType = property.PropertyType;
            if (propertyType.IsConstructedGenericType && propertyType.GetGenericTypeDefinition() == typeof(FieldValue<>))
            {
                var fieldType = propertyType.GetGenericArguments()[0];
                ModifyFieldValueProperty(property, fieldType);
            }
        }

        static void ModifyFieldValueProperty(JsonPropertyInfo property, Type fieldType)
        {
            var converterType = typeof(FieldValue<>.JsonConverter).MakeGenericType(fieldType);
            var fieldTypeInfo = property.Options.GetTypeInfo(fieldType).Converter;
            var converter = (JsonConverter)Activator.CreateInstance(converterType, [fieldTypeInfo])!;

            property.CustomConverter = converter;
            property.IsRequired = false;
            property.ShouldSerialize = ((IFieldValueConverter)converter).CreateShouldSerialize(property.ShouldSerialize);
        }
    }

    /// <summary>
    /// Helpers for modifying <see cref="JsonTypeInfo"/> for <see cref="FieldValue{T}"/>.
    /// </summary>
    internal interface IFieldValueConverter
    {
        /// <summary>
        /// Creates a delegate that determines whether the property should be serialized.
        /// </summary>
        /// <param name="inner">The original <see cref="JsonPropertyInfo.ShouldSerialize"/> delegate.</param>
        /// <returns>A delegate that determines whether a property should be serialized.</returns>
        Func<object, object?, bool> CreateShouldSerialize(Func<object, object?, bool>? inner);
    }
}

/// <summary>
/// Represents a field value (typically a database field).
/// 
/// This is similar to <see cref="Nullable{T}"/>, but with an additional state for unset values.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct FieldValue<T>
    : IEqualityOperators<FieldValue<T>, FieldValue<T>, bool>
    where T : notnull
{
    /// <summary>
    /// Represents an unset field value.
    /// </summary>
    public static readonly FieldValue<T> Unset = new(FieldState.Unset, default);

    /// <summary>
    /// Represents a null field value.
    /// </summary>
    public static readonly FieldValue<T> Null = new(FieldState.Null, default);

    private readonly FieldState _state;
    private readonly T? _value;

    private FieldValue(FieldState state, T? value)
    {
        _state = state;
        _value = value;
    }

    /// <summary>
    /// Gets whether the field is unset.
    /// </summary>
    public bool IsUnset => _state == FieldState.Unset;

    /// <summary>
    /// Gets whether the field is set.
    /// </summary>
    public bool IsSet => _state != FieldState.Unset;

    /// <summary>
    /// Gets whether the field is null.
    /// </summary>
    public bool IsNull => _state == FieldState.Null;

    /// <summary>
    /// Gets whether the field has a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue => _state == FieldState.NonNull;

    /// <summary>
    /// Gets the field value.
    /// </summary>
    public T? Value => _value;

    /// <summary>
    /// Gets the field value or a default value if the field is null/unset.
    /// </summary>
    /// <returns>The field value, or <see langword="default"/>.</returns>
    public T? OrDefault(T? defaultValue = default)
        => HasValue ? _value : defaultValue;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (IsNull && obj is null)
        {
            return true;
        }

        return obj is FieldValue<T> other && Equals(this, other);

        static bool Equals(FieldValue<T> left, FieldValue<T> right)
        {
            if (left._state != right._state)
            {
                return false;
            }

            if (left._state == FieldState.NonNull)
            {
                return EqualityComparer<T>.Default.Equals(left._value!, right._value!);
            }

            return true;
        }
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (IsNull)
        {
            return 0;
        }

        if (IsUnset)
        {
            return -1;
        }

        return _value!.GetHashCode();
    }

    /// <inheritdoc/>
    public override string? ToString()
        => _state switch
        {
            FieldState.Unset => "<unset>",
            FieldState.Null => "<null>",
            FieldState.NonNull => _value!.ToString() ?? string.Empty,
            _ => throw new UnreachableException(),
        };

    private string DebuggerDisplay
        => _state switch
        {
            FieldState.Unset => "<unset>",
            FieldState.Null => "<null>",
            FieldState.NonNull => _value!.ToString() ?? string.Empty,
            _ => throw new UnreachableException(),
        };

    /// <summary>
    /// Converts from a <see cref="FieldValue.UnsetSentinel"/> to a <see cref="FieldValue{T}"/> in the unset state.
    /// </summary>
    public static implicit operator FieldValue<T>(FieldValue.UnsetSentinel _)
        => Unset;

    /// <summary>
    /// Converts from a <see cref="FieldValue.NullSentinel"/> to a <see cref="FieldValue{T}"/> in the null state.
    /// </summary>
    public static implicit operator FieldValue<T>(FieldValue.NullSentinel _)
        => Null;

    /// <summary>
    /// Converts from a <typeparamref name="T"/> to a <see cref="FieldValue{T}"/> in the set or null state.
    /// </summary>
    /// <param name="value">The field value.</param>
    public static implicit operator FieldValue<T>(T? value)
        => value is null ? Null : new FieldValue<T>(FieldState.NonNull, value);

    /// <summary>
    /// Converts from a <see cref="FieldValue{T}"/> to a <typeparamref name="T"/>.
    /// </summary>
    /// <param name="value">The <see cref="FieldValue{T}"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown if the field value is unset or null.</exception>
    public static explicit operator T(FieldValue<T> value)
    {
        if (!value.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("FieldValue has no value");
        }

        return value.Value;
    }

    /// <inheritdoc/>
    public static bool operator ==(FieldValue<T> left, FieldValue<T> right)
        => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(FieldValue<T> left, FieldValue<T> right)
        => !left.Equals(right);

    private enum FieldState : byte
    {
        Unset = default,
        Null,
        NonNull,
    }

    /// <summary>
    /// Json converter for <see cref="FieldValue{T}"/>.
    /// </summary>
    internal class JsonConverter
        : JsonConverter<FieldValue<T>>
        , FieldValue.IFieldValueConverter
    {
        private readonly JsonConverter<T> _fieldConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="FieldValue{T}.JsonConverter"/> class.
        /// </summary>
        /// <param name="fieldConverter">The <see cref="JsonConverter{T}"/> for the field type.</param>
        public JsonConverter(JsonConverter<T> fieldConverter)
        {
            _fieldConverter = fieldConverter;
        }

        /// <inheritdoc/>
        public override bool HandleNull => true;

        /// <inheritdoc/>
        public Func<object, object?, bool> CreateShouldSerialize(Func<object, object?, bool>? inner)
        {
            if (inner is null)
            {
                return (parent, value) => !((FieldValue<T>)value!).IsUnset;
            }

            return (parent, value) => !((FieldValue<T>)value!).IsUnset && inner(parent, value);
        }

        /////// <inheritdoc/>
        ////public bool ShouldSerialize(object parent, object? value)
        ////    => !((FieldValue<T>)value!).IsUnset;

        /// <inheritdoc/>
        public override FieldValue<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Null;
            }

            return _fieldConverter.Read(ref reader, typeof(T), options) switch
            {
                null => Null,
                T value => value,
            };
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, FieldValue<T> value, JsonSerializerOptions options)
        {
            if (value.IsUnset)
            {
                throw new JsonException("Cannot serialize an unset field value");
            }

            if (value.IsNull)
            {
                writer.WriteNullValue();
                return;
            }

            _fieldConverter.Write(writer, value._value!, options);
        }
    }
}
