#nullable enable

using Altinn.Register.Core.Utils;

namespace Altinn.Register.Tests.UnitTests;

public class EnumExtensionsTests
{
    [Fact]
    public void IsDefault()
    {
        Assert.True(Bytes.None.IsDefault());
        Assert.True(SBytes.None.IsDefault());
        Assert.True(Shorts.None.IsDefault());
        Assert.True(UShorts.None.IsDefault());
        Assert.True(Ints.None.IsDefault());
        Assert.True(UInts.None.IsDefault());
        Assert.True(Longs.None.IsDefault());
        Assert.True(ULongs.None.IsDefault());

        Assert.False(Bytes.A.IsDefault());
        Assert.False(SBytes.A.IsDefault());
        Assert.False(Shorts.A.IsDefault());
        Assert.False(UShorts.A.IsDefault());
        Assert.False(Ints.A.IsDefault());
        Assert.False(UInts.A.IsDefault());
        Assert.False(Longs.A.IsDefault());
        Assert.False(ULongs.A.IsDefault());
    }

    [Fact]
    public void HasAnyFlags()
    {
        CheckImpl(Bytes.None, Bytes.A, Bytes.B, Bytes.C, Bytes.AB, Bytes.BC, Bytes.AC, Bytes.ABC);
        CheckImpl(SBytes.None, SBytes.A, SBytes.B, SBytes.C, SBytes.AB, SBytes.BC, SBytes.AC, SBytes.ABC);
        CheckImpl(Shorts.None, Shorts.A, Shorts.B, Shorts.C, Shorts.AB, Shorts.BC, Shorts.AC, Shorts.ABC);
        CheckImpl(UShorts.None, UShorts.A, UShorts.B, UShorts.C, UShorts.AB, UShorts.BC, UShorts.AC, UShorts.ABC);
        CheckImpl(Ints.None, Ints.A, Ints.B, Ints.C, Ints.AB, Ints.BC, Ints.AC, Ints.ABC);
        CheckImpl(UInts.None, UInts.A, UInts.B, UInts.C, UInts.AB, UInts.BC, UInts.AC, UInts.ABC);
        CheckImpl(Longs.None, Longs.A, Longs.B, Longs.C, Longs.AB, Longs.BC, Longs.AC, Longs.ABC);
        CheckImpl(ULongs.None, ULongs.A, ULongs.B, ULongs.C, ULongs.AB, ULongs.BC, ULongs.AC, ULongs.ABC);

        static void CheckImpl<T>(
            T none,
            T a,
            T b,
            T c,
            T ab,
            T bc,
            T ac,
            T abc)
            where T : struct, Enum
        {
            Assert.False(a.HasAnyFlags(none));
            Assert.False(b.HasAnyFlags(none));
            Assert.False(c.HasAnyFlags(none));

            Assert.False(none.HasAnyFlags(a));
            Assert.False(none.HasAnyFlags(b));
            Assert.False(none.HasAnyFlags(c));

            Assert.True(ab.HasAnyFlags(a));
            Assert.True(ab.HasAnyFlags(b));
            Assert.False(ab.HasAnyFlags(c));
            Assert.True(ab.HasAnyFlags(ab));
            Assert.True(ab.HasAnyFlags(bc));
            Assert.True(ab.HasAnyFlags(ac));
            Assert.True(ab.HasAnyFlags(abc));

            Assert.True(a.HasAnyFlags(abc));
            Assert.False(a.HasAnyFlags(bc));
        }
    }

    [Fact]
    public void BitwiseOr()
    {
        CheckImpl(Bytes.None, Bytes.A, Bytes.B, Bytes.C, Bytes.AB, Bytes.BC, Bytes.AC, Bytes.ABC);
        CheckImpl(SBytes.None, SBytes.A, SBytes.B, SBytes.C, SBytes.AB, SBytes.BC, SBytes.AC, SBytes.ABC);
        CheckImpl(Shorts.None, Shorts.A, Shorts.B, Shorts.C, Shorts.AB, Shorts.BC, Shorts.AC, Shorts.ABC);
        CheckImpl(UShorts.None, UShorts.A, UShorts.B, UShorts.C, UShorts.AB, UShorts.BC, UShorts.AC, UShorts.ABC);
        CheckImpl(Ints.None, Ints.A, Ints.B, Ints.C, Ints.AB, Ints.BC, Ints.AC, Ints.ABC);
        CheckImpl(UInts.None, UInts.A, UInts.B, UInts.C, UInts.AB, UInts.BC, UInts.AC, UInts.ABC);
        CheckImpl(Longs.None, Longs.A, Longs.B, Longs.C, Longs.AB, Longs.BC, Longs.AC, Longs.ABC);
        CheckImpl(ULongs.None, ULongs.A, ULongs.B, ULongs.C, ULongs.AB, ULongs.BC, ULongs.AC, ULongs.ABC);

        static void CheckImpl<T>(
            T none,
            T a,
            T b,
            T c,
            T ab,
            T bc,
            T ac,
            T abc)
            where T : struct, Enum
        {
            Assert.Equal(ab, a.BitwiseOr(b));
            Assert.Equal(ac, a.BitwiseOr(c));
            Assert.Equal(bc, b.BitwiseOr(c));

            Assert.Equal(ab, a.BitwiseOr(ab));
            Assert.Equal(ac, a.BitwiseOr(ac));
            Assert.Equal(abc, a.BitwiseOr(abc));

            Assert.Equal(a, a.BitwiseOr(none));
            Assert.Equal(a, a.BitwiseOr(a));

            Assert.Equal(none, none.BitwiseOr(none));
        }
    }

    [Fact]
    public void RemoveFlags()
    {
        CheckImpl(Bytes.None, Bytes.A, Bytes.B, Bytes.C, Bytes.AB, Bytes.BC, Bytes.AC, Bytes.ABC);
        CheckImpl(SBytes.None, SBytes.A, SBytes.B, SBytes.C, SBytes.AB, SBytes.BC, SBytes.AC, SBytes.ABC);
        CheckImpl(Shorts.None, Shorts.A, Shorts.B, Shorts.C, Shorts.AB, Shorts.BC, Shorts.AC, Shorts.ABC);
        CheckImpl(UShorts.None, UShorts.A, UShorts.B, UShorts.C, UShorts.AB, UShorts.BC, UShorts.AC, UShorts.ABC);
        CheckImpl(Ints.None, Ints.A, Ints.B, Ints.C, Ints.AB, Ints.BC, Ints.AC, Ints.ABC);
        CheckImpl(UInts.None, UInts.A, UInts.B, UInts.C, UInts.AB, UInts.BC, UInts.AC, UInts.ABC);
        CheckImpl(Longs.None, Longs.A, Longs.B, Longs.C, Longs.AB, Longs.BC, Longs.AC, Longs.ABC);
        CheckImpl(ULongs.None, ULongs.A, ULongs.B, ULongs.C, ULongs.AB, ULongs.BC, ULongs.AC, ULongs.ABC);

        static void CheckImpl<T>(
            T none,
            T a,
            T b,
            T c,
            T ab,
            T bc,
            T ac,
            T abc)
            where T : struct, Enum
        {
            Assert.Equal(none, none.RemoveFlags(a));
            Assert.Equal(none, a.RemoveFlags(a));

            Assert.Equal(b, ab.RemoveFlags(a));
            Assert.Equal(c, abc.RemoveFlags(ab));

            Assert.Equal(a, ab.RemoveFlags(bc));
            Assert.Equal(a, ac.RemoveFlags(bc));
        }
    }

    [Fact]
    public void NumBitsSet()
    {
        CheckImpl(Bytes.None, Bytes.A, Bytes.B, Bytes.C, Bytes.AB, Bytes.BC, Bytes.AC, Bytes.ABC);
        CheckImpl(SBytes.None, SBytes.A, SBytes.B, SBytes.C, SBytes.AB, SBytes.BC, SBytes.AC, SBytes.ABC);
        CheckImpl(Shorts.None, Shorts.A, Shorts.B, Shorts.C, Shorts.AB, Shorts.BC, Shorts.AC, Shorts.ABC);
        CheckImpl(UShorts.None, UShorts.A, UShorts.B, UShorts.C, UShorts.AB, UShorts.BC, UShorts.AC, UShorts.ABC);
        CheckImpl(Ints.None, Ints.A, Ints.B, Ints.C, Ints.AB, Ints.BC, Ints.AC, Ints.ABC);
        CheckImpl(UInts.None, UInts.A, UInts.B, UInts.C, UInts.AB, UInts.BC, UInts.AC, UInts.ABC);
        CheckImpl(Longs.None, Longs.A, Longs.B, Longs.C, Longs.AB, Longs.BC, Longs.AC, Longs.ABC);
        CheckImpl(ULongs.None, ULongs.A, ULongs.B, ULongs.C, ULongs.AB, ULongs.BC, ULongs.AC, ULongs.ABC);

        static void CheckImpl<T>(
            T none,
            T a,
            T b,
            T c,
            T ab,
            T bc,
            T ac,
            T abc)
            where T : struct, Enum
        {
            Assert.Equal(0, none.NumBitsSet());
            Assert.Equal(1, a.NumBitsSet());
            Assert.Equal(1, b.NumBitsSet());
            Assert.Equal(1, c.NumBitsSet());
            Assert.Equal(2, ab.NumBitsSet());
            Assert.Equal(2, bc.NumBitsSet());
            Assert.Equal(2, ac.NumBitsSet());
            Assert.Equal(3, abc.NumBitsSet());
        }
    }

    [Flags]
    private enum Bytes : byte
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }

    [Flags]
    private enum SBytes : sbyte
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }

    [Flags]
    private enum Shorts : short
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }

    [Flags]
    private enum UShorts : ushort
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }

    [Flags]
    private enum Ints : int
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }

    [Flags]
    private enum UInts : uint
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }

    [Flags]
    private enum Longs : long
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }

    [Flags]
    private enum ULongs : ulong
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,

        AB = A | B,
        BC = B | C,
        AC = A | C,

        ABC = A | B | C,
    }
}
