using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CommunityToolkit.Diagnostics;

/// <summary>
/// Extension methods for <see cref="Guard"/> and <see cref="ThrowHelper"/> in Altinn Register Core.
/// </summary>
public static class AltinnRegisterCoreGuardExtensions
{
    extension(ThrowHelper)
    {
        /// <summary>
        /// Throws a new <see cref="InvalidCastException"/>.
        /// </summary>
        /// <param name="message">The message to include in the exception.</param>
        /// <exception cref="InvalidCastException">Thrown with the specified parameter.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidCastException(string message)
            => throw new InvalidCastException(message);

        /// <summary>
        /// Throws a new <see cref="InvalidCastException"/>.
        /// </summary>
        /// <param name="message">The message to include in the exception.</param>
        /// <exception cref="InvalidCastException">Thrown with the specified parameter.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowInvalidCastException<T>(string message)
            where T : allows ref struct
            => throw new InvalidCastException(message);
    }
}
