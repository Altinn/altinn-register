using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Altinn.Authorization.ModelUtils;

namespace Shouldly;

[DebuggerStepThrough]
[ShouldlyMethods]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FieldValueShouldExtensions
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBeNull<T>(this FieldValue<T> actual, string? customMessage = null)
        where T : notnull
    {
        // https://github.com/shouldly/shouldly/issues/1092
        actual.AssertAwesomely(static v => v.IsNull, FieldValue<T>.Null, actual, customMessage);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBeUnset<T>(this FieldValue<T> actual, string? customMessage = null)
        where T : notnull
    {
        actual.AssertAwesomely(static v => v.IsUnset, actual, FieldValue<T>.Unset, customMessage);
    }
}
