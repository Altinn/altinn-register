using Altinn.Authorization.ProblemDetails;

namespace Altinn.Register.Core.Errors;

/// <summary>
/// Extensions for validation errors.
/// </summary>
public static class ValidationErrorExtensions
{
    // TODO: update utils with this functionality

    /// <summary>
    /// Creates a <see cref="ProblemInstance"/> from a single <see cref="ValidationErrorInstance"/>.
    /// This is useful for cases where only a single validation error is expected, and allows for
    /// more concise code when creating problem details for validation errors.
    /// </summary>
    /// <param name="instance">The <see cref="ValidationErrorInstance"/>.</param>
    /// <returns>A <see cref="ProblemInstance"/>.</returns>
    public static ValidationProblemInstance ToProblemInstance(this ValidationErrorInstance instance)
    {
        ValidationErrorBuilder builder = default;
        builder.Add(instance);
        builder.TryBuild(out var result);
        return result!;
    }
}
