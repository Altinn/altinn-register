#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Altinn.Register.Core.Utils;

/// <summary>
/// Defines a mechanism for converting a value of type <typeparamref name="TSource"/> to a value of type <typeparamref name="TSelf"/>.
/// </summary>
/// <typeparam name="TSelf">The type that implements this interface.</typeparam>
/// <typeparam name="TSource">The source type to convert from.</typeparam>
public interface IConvertibleFrom<TSelf, TSource>
    where TSelf : IConvertibleFrom<TSelf, TSource>?
{
    /// <summary>
    /// Tries to convert a <typeparamref name="TSource"/> to a <typeparamref name="TSelf"/>.
    /// </summary>
    /// <param name="source">The source to convert from.</param>
    /// <param name="result">On return, contains the result of successfully converting <paramref name="source"/> or an undefined value on failure.</param>
    /// <returns><see langword="true"/> if <paramref name="source"/> was successfully converted; otherwise, <see langword="false"/>.</returns>
    static abstract bool TryConvertFrom([NotNullWhen(true)] TSource? source, [MaybeNullWhen(false)] out TSelf result);
}
