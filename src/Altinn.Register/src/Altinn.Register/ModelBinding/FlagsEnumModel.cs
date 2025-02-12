#nullable enable

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Register.Core.Utils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.ModelBinding;

/// <summary>
/// Static helpers for <see cref="FlagsEnumModel{TEnum}"/>.
/// </summary>
public static class FlagsEnumModel
{
    /// <summary>
    /// Creates a new <see cref="FlagsEnumModel{TEnum}"/>.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="namingPolicy">The naming policy.</param>
    /// <param name="comparison">The string comparison to use.</param>
    /// <returns>A new model.</returns>
    public static FlagsEnumModel<T> Create<T>(JsonNamingPolicy namingPolicy, StringComparison comparison)
        where T : struct, Enum
        => FlagsEnumModel<T>.Create(namingPolicy, comparison);
}

/// <summary>
/// A model for a flags enum.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public sealed class FlagsEnumModel<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Creates a new <see cref="FlagsEnumModel{TEnum}"/>.
    /// </summary>
    /// <param name="namingPolicy">The naming policy.</param>
    /// <param name="comparison">The string comparison to use.</param>
    /// <returns>A new model.</returns>
    public static FlagsEnumModel<TEnum> Create(JsonNamingPolicy namingPolicy, StringComparison comparison)
    {
        var names = Enum.GetNames<TEnum>();
        var values = Enum.GetValues<TEnum>();

        var noneFound = false;
        var builder = ImmutableArray.CreateBuilder<Item>(names.Length - 1);
        for (int i = 0; i < names.Length; i++)
        {
            var value = values[i];
            var fieldName = names[i];
            var baseName = fieldName;
            var field = typeof(TEnum).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            if (field?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>() is { } attr)
            {
                baseName = attr.Name;
            }

            var name = namingPolicy.ConvertName(baseName);

            if (value.Equals(default(TEnum)))
            {
                Debug.Assert(fieldName == "None");
                noneFound = true;
                continue;
            }

            builder.Add(new Item(value, name));
        }

        if (!noneFound)
        {
            Debug.Fail($"Enum '{typeof(TEnum).Name}' does not have a 'None' value.");
        }

        // Sort by number of bits set, greatest number of bits set first.
        builder.Sort(static (a, b) =>
        {
            var aBits = a.Value.NumBitsSet();
            var bBits = b.Value.NumBitsSet();

            return bBits.CompareTo(aBits);
        });

        return new(builder.DrainToImmutable(), comparison);
    }

    private readonly ImmutableArray<Item> _items;
    private readonly StringComparison _comparison;
    private readonly ConcurrentDictionary<TEnum, string> _formatted = new();

    private FlagsEnumModel(ImmutableArray<Item> items, StringComparison comparison)
    {
        _items = items;
        _comparison = comparison;
    }

    /// <summary>
    /// Gets the items in the model.
    /// </summary>
    public ImmutableArray<Item> Items => _items;

    /// <summary>
    /// Tries to parse a <see cref="ReadOnlySpan{T}"/> of characters to an enum value.
    /// </summary>
    /// <param name="name">The name to match.</param>
    /// <param name="value">Output value. Set to <see langword="default"/> if name is not found.</param>
    /// <returns><see langword="true"/> if the parsing was successful, otherwise <see langword="false"/>.</returns>
    public bool TryParse(ReadOnlySpan<char> name, out TEnum value)
    {
        foreach (ref readonly var item in _items.AsSpan())
        {
            if (name.Equals(item.Name, _comparison))
            {
                value = item.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Formats a <typeparamref name="TEnum"/>.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted string.</returns>
    public string Format(TEnum value)
        => _formatted.GetOrAdd(value, FormatCore);

    private string FormatCore(TEnum value)
    {
        if (value.IsDefault())
        {
            return string.Empty;
        }

        var items = new List<Item>();

        foreach (ref readonly var item in _items.AsSpan())
        {
            if (value.HasFlag(item.Value))
            {
                items.Add(item);
                value = value.RemoveFlags(item.Value);

                if (value.IsDefault())
                {
                    break;
                }
            }
        }

        if (!value.IsDefault())
        {
            ThrowHelper.ThrowArgumentException(nameof(value), "Not all bits in the enum are named");
        }

        items.Sort(static (a, b) => Comparer<TEnum>.Default.Compare(a.Value, b.Value));

        return string.Join(',', items.Select(static i => i.Name));
    }

    /// <summary>
    /// An item in the model.
    /// </summary>
    /// <param name="Value">The value of the item.</param>
    /// <param name="Name">The name of the item.</param>
    public readonly record struct Item(TEnum Value, string Name);
}
