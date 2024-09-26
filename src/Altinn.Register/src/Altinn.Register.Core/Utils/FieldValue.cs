using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
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
    public T? OrDefault()
        => HasValue ? _value : default;

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
}
