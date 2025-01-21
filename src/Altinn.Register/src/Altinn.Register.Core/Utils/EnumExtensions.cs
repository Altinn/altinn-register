using System.Numerics;
using System.Runtime.CompilerServices;

namespace Altinn.Register.Core.Utils;

/// <summary>
/// Extensions for enums.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Determines whether the value has any bits set.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The value to check for flags.</param>
    /// <returns><see langword="true"/> all bits are unset.</returns>
    /// <exception cref="InvalidOperationException">If the underlying type of <typeparamref name="T"/> is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsDefault<T>(this T value)
        where T : struct, Enum
    {
        if (typeof(T).GetEnumUnderlyingType() == typeof(byte))
        {
            return ByteEnumIsNone(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(sbyte))
        {
            return ByteEnumIsNone(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(short))
        {
            return ShortEnumIsNone(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ushort))
        {
            return ShortEnumIsNone(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(int))
        {
            return IntEnumIsNone(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(uint))
        {
            return IntEnumIsNone(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(long))
        {
            return LongEnumIsNone(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ulong))
        {
            return LongEnumIsNone(value);
        }

        throw new InvalidOperationException($"Enums with underlying type {Enum.GetUnderlyingType(typeof(T))} not supported.");
    }

    /// <summary>
    /// Determines whether the value has any of the specified flags set.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The value to check for flags.</param>
    /// <param name="flags">The flags to match against.</param>
    /// <returns><see langword="true"/> if any of the bits in <paramref name="flags"/> is also set in <paramref name="value"/>.</returns>
    /// <exception cref="InvalidOperationException">If the underlying type of <typeparamref name="T"/> is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool HasAnyFlags<T>(this T value, T flags)
        where T : struct, Enum
    {
        if (typeof(T).GetEnumUnderlyingType() == typeof(byte))
        {
            return ByteEnumHasAnyFlags(value, flags);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(sbyte))
        {
            return ByteEnumHasAnyFlags(value, flags);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(short))
        {
            return ShortEnumHasAnyFlags(value, flags);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ushort))
        {
            return ShortEnumHasAnyFlags(value, flags);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(int))
        {
            return IntEnumHasAnyFlags(value, flags);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(uint))
        {
            return IntEnumHasAnyFlags(value, flags);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(long))
        {
            return LongEnumHasAnyFlags(value, flags);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ulong))
        {
            return LongEnumHasAnyFlags(value, flags);
        }

        throw new InvalidOperationException($"Enums with underlying type {Enum.GetUnderlyingType(typeof(T))} not supported.");
    }

    /// <summary>
    /// Computes the bitwise-or of two values.
    /// </summary>
    /// <remarks>
    /// This is the same as using the <see cref="IBitwiseOperators{TSelf, TOther, TResult}.op_BitwiseOr(TSelf, TOther)"/>,
    /// except it can be used in generic context.
    /// </remarks>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="left">The value to or with <paramref name="right"/>.</param>
    /// <param name="right">The value to or with <paramref name="left"/>.</param>
    /// <returns>The bitwise-or of <paramref name="left" /> and <paramref name="right" />.
    /// <exception cref="InvalidOperationException">If the underlying type of <typeparamref name="T"/> is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T BitwiseOr<T>(this T left, T right)
        where T : struct, Enum
    {
        if (typeof(T).GetEnumUnderlyingType() == typeof(byte))
        {
            return ByteEnumBitwiseOr(left, right);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(sbyte))
        {
            return ByteEnumBitwiseOr(left, right);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(short))
        {
            return ShortEnumBitwiseOr(left, right);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ushort))
        {
            return ShortEnumBitwiseOr(left, right);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(int))
        {
            return IntEnumBitwiseOr(left, right);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(uint))
        {
            return IntEnumBitwiseOr(left, right);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(long))
        {
            return LongEnumBitwiseOr(left, right);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ulong))
        {
            return LongEnumBitwiseOr(left, right);
        }

        throw new InvalidOperationException($"Enums with underlying type {Enum.GetUnderlyingType(typeof(T))} not supported.");
    }

    /// <summary>
    /// Computes the bitwise-or of two values.
    /// </summary>
    /// <remarks>
    /// This is the same as using the <see cref="IBitwiseOperators{TSelf, TOther, TResult}.op_BitwiseOr(TSelf, TOther)"/>,
    /// except it can be used in generic context.
    /// </remarks>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The value to remove <paramref name="toRemove"/> from.</param>
    /// <param name="toRemove">The value to remove from <paramref name="value"/>.</param>
    /// <returns>All the flags in <paramref name="value" /> minus the flags in <paramref name="toRemove" />.
    /// <exception cref="InvalidOperationException">If the underlying type of <typeparamref name="T"/> is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T RemoveFlags<T>(this T value, T toRemove)
        where T : struct, Enum
    {
        if (typeof(T).GetEnumUnderlyingType() == typeof(byte))
        {
            return ByteEnumRemoveFlags(value, toRemove);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(sbyte))
        {
            return ByteEnumRemoveFlags(value, toRemove);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(short))
        {
            return ShortEnumRemoveFlags(value, toRemove);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ushort))
        {
            return ShortEnumRemoveFlags(value, toRemove);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(int))
        {
            return IntEnumRemoveFlags(value, toRemove);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(uint))
        {
            return IntEnumRemoveFlags(value, toRemove);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(long))
        {
            return LongEnumRemoveFlags(value, toRemove);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ulong))
        {
            return LongEnumRemoveFlags(value, toRemove);
        }

        throw new InvalidOperationException($"Enums with underlying type {Enum.GetUnderlyingType(typeof(T))} not supported.");
    }

    /// <summary>
    /// Gets the number of bits set in the value.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The value to check bits set.</param>
    /// <returns>The number of bits set in <paramref name="value"/>.</returns>
    /// <exception cref="InvalidOperationException">If the underlying type of <typeparamref name="T"/> is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int NumBitsSet<T>(this T value)
        where T : struct, Enum
    {
        if (typeof(T).GetEnumUnderlyingType() == typeof(byte))
        {
            return ByteEnumBitsSet(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(sbyte))
        {
            return ByteEnumBitsSet(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(short))
        {
            return ShortEnumBitsSet(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ushort))
        {
            return ShortEnumBitsSet(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(int))
        {
            return IntEnumBitsSet(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(uint))
        {
            return IntEnumBitsSet(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(long))
        {
            return LongEnumBitsSet(value);
        }

        if (typeof(T).GetEnumUnderlyingType() == typeof(ulong))
        {
            return LongEnumBitsSet(value);
        }

        throw new InvalidOperationException($"Enums with underlying type {Enum.GetUnderlyingType(typeof(T))} not supported.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ByteEnumIsNone<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, byte>(ref value);
        return valueByte == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShortEnumIsNone<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, ushort>(ref value);
        return valueByte == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IntEnumIsNone<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, uint>(ref value);
        return valueByte == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LongEnumIsNone<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, ulong>(ref value);
        return valueByte == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ByteEnumHasAnyFlags<T>(T value, T flags)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, byte>(ref value);
        var flagsByte = Unsafe.As<T, byte>(ref flags);
        return (valueByte & flagsByte) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShortEnumHasAnyFlags<T>(T value, T flags)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, ushort>(ref value);
        var flagsByte = Unsafe.As<T, ushort>(ref flags);
        return (valueByte & flagsByte) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IntEnumHasAnyFlags<T>(T value, T flags)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, uint>(ref value);
        var flagsByte = Unsafe.As<T, uint>(ref flags);
        return (valueByte & flagsByte) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LongEnumHasAnyFlags<T>(T value, T flags)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, ulong>(ref value);
        var flagsByte = Unsafe.As<T, ulong>(ref flags);
        return (valueByte & flagsByte) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ByteEnumBitwiseOr<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, byte>(ref left);
        var rightByte = Unsafe.As<T, byte>(ref right);
        var resultByte = (byte)(leftByte | rightByte);
        return Unsafe.As<byte, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ShortEnumBitwiseOr<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, ushort>(ref left);
        var rightByte = Unsafe.As<T, ushort>(ref right);
        var resultByte = (ushort)(leftByte | rightByte);
        return Unsafe.As<ushort, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T IntEnumBitwiseOr<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, uint>(ref left);
        var rightByte = Unsafe.As<T, uint>(ref right);
        var resultByte = leftByte | rightByte;
        return Unsafe.As<uint, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T LongEnumBitwiseOr<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, ulong>(ref left);
        var rightByte = Unsafe.As<T, ulong>(ref right);
        var resultByte = leftByte | rightByte;
        return Unsafe.As<ulong, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ByteEnumRemoveFlags<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, byte>(ref left);
        var rightByte = Unsafe.As<T, byte>(ref right);
        var resultByte = (byte)(leftByte & ~rightByte);
        return Unsafe.As<byte, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ShortEnumRemoveFlags<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, ushort>(ref left);
        var rightByte = Unsafe.As<T, ushort>(ref right);
        var resultByte = (ushort)(leftByte & ~rightByte);
        return Unsafe.As<ushort, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T IntEnumRemoveFlags<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, uint>(ref left);
        var rightByte = Unsafe.As<T, uint>(ref right);
        var resultByte = leftByte & ~rightByte;
        return Unsafe.As<uint, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T LongEnumRemoveFlags<T>(T left, T right)
        where T : struct, Enum
    {
        var leftByte = Unsafe.As<T, ulong>(ref left);
        var rightByte = Unsafe.As<T, ulong>(ref right);
        var resultByte = leftByte & ~rightByte;
        return Unsafe.As<ulong, T>(ref resultByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ByteEnumBitsSet<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, byte>(ref value);
        return BitOperations.PopCount(valueByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ShortEnumBitsSet<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, ushort>(ref value);
        return BitOperations.PopCount(valueByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IntEnumBitsSet<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, uint>(ref value);
        return BitOperations.PopCount(valueByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LongEnumBitsSet<T>(T value)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, ulong>(ref value);
        return BitOperations.PopCount(valueByte);
    }
}
