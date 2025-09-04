using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Register.TestUtils.Assertions;

/// <summary>
/// Base class for custom assertions.
/// </summary>
/// <typeparam name="T">The type being asserted over.</typeparam>
/// <param name="subject">The subject of assertions.</param>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
public abstract class CustomAssertions<T>(T subject)
{
    /// <summary>
    /// Gets the field value which is being asserted.
    /// </summary>
    public T Subject { get; } = subject;

    /// <summary>
    /// Used in error messages to identify the subject being asserted.
    /// </summary>
    protected abstract string Identifier { get; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        throw new NotSupportedException("Equals is not part of Fluent Assertions. Did you mean Be() instead?");

    /// <inheritdoc/>
    public override int GetHashCode() =>
        throw new NotSupportedException("GetHashCode is not part of Fluent Assertions.");
}
