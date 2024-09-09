using System.Runtime.CompilerServices;

namespace Altinn.Register.Core.Utils;

/// <summary>
/// Extensions for enums.
/// </summary>
public static class EnumExtensions
{
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
        var valueByte = Unsafe.As<T, int>(ref value);
        var flagsByte = Unsafe.As<T, int>(ref flags);
        return (valueByte & flagsByte) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LongEnumHasAnyFlags<T>(T value, T flags)
        where T : struct, Enum
    {
        var valueByte = Unsafe.As<T, long>(ref value);
        var flagsByte = Unsafe.As<T, long>(ref flags);
        return (valueByte & flagsByte) != 0;
    }
}
