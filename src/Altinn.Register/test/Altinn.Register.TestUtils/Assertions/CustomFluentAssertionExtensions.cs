using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Utils;
using Altinn.Register.TestUtils.Assertions;

namespace FluentAssertions;

/// <summary>
/// Extension methods for FluentAssertions.
/// </summary>
public static class CustomFluentAssertionExtensions
{
    /// <summary>
    /// Returns a <see cref="FieldValueAssertions{T}"/> object that can be used to assert the
    /// current <see cref="FieldValue{T}"/>.
    /// </summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="fieldValue">The field value to assert on.</param>
    public static FieldValueAssertions<T> Should<T>(this FieldValue<T> fieldValue)
        where T : notnull
        => new(fieldValue);

    /// <summary>
    /// Returns a <see cref="ResultAssertions{T}"/> object that can be used to assert the
    /// current <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="result">The result to assert on.</param>
    public static ResultAssertions<T> Should<T>(this Result<T> result)
        where T : notnull
        => new(result);
}
