using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Register.Core.Utils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// <see cref="JsonConverter"/> for <see cref="PartyRecord"/> and subclasses.
/// </summary>
internal class PartyRecordJsonConverter
    : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsAssignableTo(typeof(PartyRecord));

    /// <inheritdoc/>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(TypedPartyRecordJsonConverter<>).MakeGenericType(typeToConvert), options)!;

    private sealed class TypedPartyRecordJsonConverter<T>
        : JsonConverter<T>
        , IPartyRecordJsonConverter<T>
        where T : PartyRecord
    {
        private readonly PropertyName _typeName;
        private readonly ImmutableArray<PropertyContract> _properties;
        private readonly Func<FieldValue<PartyType>, T> _factory;
        private readonly Func<FieldValue<PartyType>, JsonConverter, JsonSerializerOptions, IPartyRecordJsonConverter<T>> _converterLookup;

        public TypedPartyRecordJsonConverter(JsonSerializerOptions options)
        {
            var builder = ImmutableArray.CreateBuilder<PropertyContract>(PartyRecordReaderCache<T>.Properties.Length);

            var namingPolicy = options.PropertyNamingPolicy;
            foreach (var property in PartyRecordReaderCache<T>.Properties)
            {
                var propertyNameString = namingPolicy is null ? property.Name : namingPolicy.ConvertName(property.Name);
                var propertyName = new PropertyName(propertyNameString, options.PropertyNameCaseInsensitive);

                builder.Add(new PropertyContract(property, propertyName));
            }

            var typeNameString = namingPolicy is null ? nameof(PartyRecord.PartyType) : namingPolicy.ConvertName(nameof(PartyRecord.PartyType));

            _factory = PartyRecordReaderCache<T>.Factory;
            _converterLookup = PartyRecordReaderCache<T>.ConverterLookup;
            _typeName = new PropertyName(typeNameString, options.PropertyNameCaseInsensitive);
            _properties = builder.ToImmutable();
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected {typeof(T).Name} object.");
            }

            // we don't support names longer than 128 bytes
            var propertyNameScratch = ArrayPool<char>.Shared.Rent(128);
            
            try
            {
                // Note: explicitly not passing reader by ref here to read ahead and return the reader to the original position
                var partyType = FindPartyType(reader, propertyNameScratch, options);
                var converter = _converterLookup(partyType, this, options);
                return converter.ReadPartyRecord(ref reader, partyType, propertyNameScratch, options);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(propertyNameScratch);
            }
        }

        public T ReadPartyRecord(ref Utf8JsonReader reader, FieldValue<PartyType> partyType, Span<char> propertyNameScratch, JsonSerializerOptions options)
        {
            var instance = _factory(partyType);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected property name.");
                }

                var propertyNameLength = reader.CopyString(propertyNameScratch);
                if (FindProperty(propertyNameScratch[..propertyNameLength], out var property))
                {
                    reader.Read();
                    property.Property.SetValueFromJson(ref reader, instance, options);
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }

            return instance;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            var type = value.PartyType;
            if (type.IsNull)
            {
                writer.WriteNull(_typeName.Encoded);
            }
            else if (type.HasValue)
            {
                writer.WritePropertyName(_typeName.Encoded);
                JsonSerializer.Serialize(writer, type.Value, options);
            }

            foreach (var prop in _properties.AsSpan())
            {
                prop.Property.Write(writer, prop.PropertyName.Encoded, value, options);
            }

            writer.WriteEndObject();
        }

        private FieldValue<PartyType> FindPartyType(Utf8JsonReader reader, Span<char> propertyNameScratch, JsonSerializerOptions options)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected property name.");
                }

                var propertyNameLength = reader.CopyString(propertyNameScratch);
                var propertyName = propertyNameScratch[..propertyNameLength];

                if (_typeName.Equals(propertyName))
                {
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.Null)
                    {
                        return FieldValue.Null;
                    }

                    return JsonSerializer.Deserialize<PartyType>(ref reader, options);
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }

            return default;
        }

        private bool FindProperty(ReadOnlySpan<char> propertyName, out PropertyContract property)
        {
            foreach (var prop in _properties)
            {
                if (prop.PropertyName.Equals(propertyName))
                {
                    property = prop;
                    return true;
                }
            }

            property = default;
            return false;
        }

        private readonly struct PropertyContract(IPartyRecordProperty<T> property, PropertyName propertyName)
        {
            public readonly IPartyRecordProperty<T> Property = property;
            public readonly PropertyName PropertyName = propertyName;
        }

        private readonly struct PropertyName(string name, bool caseInsensitive)
        {
            public readonly JsonEncodedText Encoded = JsonEncodedText.Encode(name);
            
            public readonly bool Equals(ReadOnlySpan<char> other)
            {
                var comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return name.AsSpan().Equals(other, comparison);
            }
        }
    }

    /// <summary>
    /// Helper interface for serializing and deserializing <see cref="PartyRecord"/> and subclasses.
    /// </summary>
    /// <typeparam name="T">The party record type.</typeparam>
    private interface IPartyRecordJsonConverter<out T>
        where T : PartyRecord
    {
        /// <summary>
        /// Reads a party record from the JSON reader.
        /// </summary>
        /// <param name="reader">The json reader.</param>
        /// <param name="partyType">The <see cref="PartyRecord.PartyType"/> field value.</param>
        /// <param name="propertyNameScratch">A scratch space to temporarily write property names to avoid allocations.</param>
        /// <param name="options">The <see cref="JsonSerializerOptions"/>.</param>
        /// <returns>A <typeparamref name="T"/> that has been deserialized from the reader.</returns>
        T ReadPartyRecord(ref Utf8JsonReader reader, FieldValue<PartyType> partyType, Span<char> propertyNameScratch, JsonSerializerOptions options);
    }

    private abstract class PartyRecordReaderCache
    {
        public static PartyRecordReaderCache For(Type type)
        {
            Debug.Assert(type.IsAssignableTo(typeof(PartyRecord)));

            var cacheType = typeof(PartyRecordReaderCache<>).MakeGenericType(type);
            var instanceProperty = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!;
            return (PartyRecordReaderCache)instanceProperty.GetValue(null)!;
        }

        public abstract IReadOnlyList<IPartyRecordProperty<T>> GetProperties<T>()
            where T : PartyRecord;
    }

    private sealed class PartyRecordReaderCache<T>
        : PartyRecordReaderCache
        where T : PartyRecord
    {
        private static readonly Func<FieldValue<PartyType>, T> _factory
            = PopulateFactory();

        private static readonly Func<FieldValue<PartyType>, JsonConverter, JsonSerializerOptions, IPartyRecordJsonConverter<T>> _converterLookup
            = PopulateConverterLookup();

        private static readonly ImmutableArray<IPartyRecordProperty<T>> _properties
            = PopulateProperties();

        private static Func<FieldValue<PartyType>, JsonConverter, JsonSerializerOptions, IPartyRecordJsonConverter<T>> PopulateConverterLookup()
        {
            if (typeof(T) == typeof(PartyRecord))
            {
                return (FieldValue<PartyType> partyType, JsonConverter converter, JsonSerializerOptions options) =>
                {
                    if (!partyType.HasValue)
                    {
                        return (IPartyRecordJsonConverter<T>)converter;
                    }

                    return partyType.Value switch
                    {
                        PartyType.Person => (IPartyRecordJsonConverter<T>)options.GetConverter(typeof(PersonRecord)),
                        PartyType.Organization => (IPartyRecordJsonConverter<T>)options.GetConverter(typeof(OrganizationRecord)),
                        _ => ThrowHelper.ThrowInvalidOperationException<IPartyRecordJsonConverter<T>>($"Invalid party type: {partyType.Value}"),
                    };
                };
            }

            return (FieldValue<PartyType> PartyType, JsonConverter converter, JsonSerializerOptions options) =>
            {
                return (IPartyRecordJsonConverter<T>)converter;
            };
        }

        private static Func<FieldValue<PartyType>, T> PopulateFactory()
        {
            if (typeof(T) == typeof(PartyRecord))
            {
                return PopulatePartyRecordFactory();
            }

            var ctors = typeof(T).GetConstructors();
            if (ctors.Length != 1)
            {
                ThrowHelper.ThrowInvalidOperationException($"Type {typeof(T).Name} must have exactly one constructor.");
            }

            var ctor = ctors[0];
            var args = ctor.GetParameters();

            return args.Length switch
            {
                0 => PopulateSpecifiedTypeFactory(ctor),
                _ => ThrowHelper.ThrowInvalidOperationException<Func<FieldValue<PartyType>, T>>($"Type {typeof(T).Name} must have either 0 constructor parameters."),
            };

            static Func<FieldValue<PartyType>, T> PopulateSpecifiedTypeFactory(ConstructorInfo ctor)
            {
                var invoker = ConstructorInvoker.Create(ctor);
                var typeFieldValue = ((T)invoker.Invoke()).PartyType;

                if (!typeFieldValue.HasValue)
                {
                    ThrowHelper.ThrowInvalidOperationException($"Type {typeof(T).Name} must have a non-null party type after the constructor has been called.");
                }

                var type = typeFieldValue.Value;

                return (FieldValue<PartyType> partyType) =>
                {
                    if (partyType.HasValue && partyType.Value != type)
                    {
                        throw new JsonException($"Invalid party type. Expected: {type}, Actual: {partyType.Value}");
                    }

                    return (T)invoker.Invoke();
                };
            }

            static Func<FieldValue<PartyType>, T> PopulatePartyRecordFactory()
            {
                var ctor = typeof(T).GetConstructor([typeof(FieldValue<PartyType>)]);
                Debug.Assert(ctor is not null);

                var invoker = ConstructorInvoker.Create(ctor);
                return (FieldValue<PartyType> partyType) => (T)invoker.Invoke(partyType);
            }
        }

        private static ImmutableArray<IPartyRecordProperty<T>> PopulateProperties()
        {
            var builder = ImmutableArray.CreateBuilder<IPartyRecordProperty<T>>();
            if (typeof(T).BaseType is { } baseType && baseType.IsAssignableTo(typeof(PartyRecord)))
            {
                var baseProperties = PartyRecordReaderCache.For(baseType).GetProperties<T>();
                builder.AddRange(baseProperties);
            }

            var propertyInfos = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanWrite && p.CanRead && p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
                .ToList();

            foreach (var info in propertyInfos)
            {
                var type = info.PropertyType;
                var getter = info.GetMethod;
                var setter = info.SetMethod;

                Debug.Assert(getter is not null);
                Debug.Assert(setter is not null);
                Debug.Assert(type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(FieldValue<>));

                if (!getter.IsPublic || !setter.IsPublic)
                {
                    continue;
                }

                var fieldValueType = type.GetGenericArguments()[0];
                var propertyHelperType = typeof(PartyRecordProperty<,>).MakeGenericType(typeof(T), fieldValueType);
                var propertyHelper = Activator.CreateInstance(propertyHelperType, info)!;
                builder.Add((IPartyRecordProperty<T>)propertyHelper);
            }

            return builder.ToImmutable();
        }

        public static PartyRecordReaderCache<T> Instance { get; } = new();

        public override IReadOnlyList<IPartyRecordProperty<T2>> GetProperties<T2>()
        {
            Debug.Assert(typeof(T).IsAssignableFrom(typeof(T2)));

            return (IReadOnlyList<IPartyRecordProperty<T2>>)(IReadOnlyList<object>)_properties;
        }

        public static Func<FieldValue<PartyType>, T> Factory
            => _factory;

        public static Func<FieldValue<PartyType>, JsonConverter, JsonSerializerOptions, IPartyRecordJsonConverter<T>> ConverterLookup
            => _converterLookup;

        public static ReadOnlySpan<IPartyRecordProperty<T>> Properties
            => _properties.AsSpan();
    }

    /// <summary>
    /// A helper for reading and writing JSON properties of a <see cref="PartyRecord"/>.
    /// </summary>
    /// <typeparam name="TOwner">The party record type.</typeparam>
    private interface IPartyRecordProperty<in TOwner>
        where TOwner : PartyRecord
    {
        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Writes the property to the JSON writer.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="propertyName">The encoded property name.</param>
        /// <param name="owner">The property owner.</param>
        /// <param name="options">The <see cref="JsonSerializerOptions"/>.</param>
        void Write(Utf8JsonWriter writer, JsonEncodedText propertyName, TOwner owner, JsonSerializerOptions options);

        /// <summary>
        /// Sets the property value from the JSON reader.
        /// </summary>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="owner">The owner object.</param>
        /// <param name="options">The <see cref="JsonSerializerOptions"/>.</param>
        void SetValueFromJson(ref Utf8JsonReader reader, TOwner owner, JsonSerializerOptions options);
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private sealed class PartyRecordProperty<TOwner, TProperty>
        : IPartyRecordProperty<TOwner>
        where TOwner : PartyRecord
        where TProperty : notnull
    {
        private readonly PropertyInfo _info;
        private readonly JsonIgnoreCondition? _ignoreCondition;
        private readonly Func<TOwner, FieldValue<TProperty>> _getter;
        private readonly Action<TOwner, FieldValue<TProperty>> _setter;

        public PartyRecordProperty(PropertyInfo propertyInfo)
        {
            Debug.Assert(propertyInfo.GetMethod is not null);
            Debug.Assert(propertyInfo.SetMethod is not null);

            _info = propertyInfo;
            _ignoreCondition = propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition;
            _getter = propertyInfo.GetMethod.CreateDelegate<Func<TOwner, FieldValue<TProperty>>>();
            _setter = propertyInfo.SetMethod.CreateDelegate<Action<TOwner, FieldValue<TProperty>>>();
        }

        public string Name
            => _info.Name;

        private string DebuggerDisplay
            => $"{typeof(TProperty).Name} {typeof(TOwner).Name}.{Name}";

        private JsonIgnoreCondition IgnoreCondition(JsonSerializerOptions options)
            => _ignoreCondition ?? options.DefaultIgnoreCondition;

        public void SetValueFromJson(ref Utf8JsonReader reader, TOwner owner, JsonSerializerOptions options)
        {
            var ignoreCondition = IgnoreCondition(options);
            if (ignoreCondition is JsonIgnoreCondition.Always)
            {
                reader.Skip();
                return;
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                _setter(owner, FieldValue.Null);
                return;
            }

            var value = JsonSerializer.Deserialize<TProperty>(ref reader, options);
            _setter(owner, value);
        }

        public void Write(Utf8JsonWriter writer, JsonEncodedText propertyName, TOwner owner, JsonSerializerOptions options)
        {
            var ignoreCondition = IgnoreCondition(options);
            if (ignoreCondition is JsonIgnoreCondition.Always)
            {
                return;
            }

            var value = _getter(owner);
            if (value.IsUnset)
            {
                return;
            }

            if (ignoreCondition is JsonIgnoreCondition.WhenWritingNull && value.IsNull)
            {
                return;
            }

            writer.WritePropertyName(propertyName);
         
            if (value.IsNull)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
