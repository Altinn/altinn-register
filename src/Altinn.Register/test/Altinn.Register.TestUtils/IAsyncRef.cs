using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.TestUtils;

/// <summary>
/// An asynchronous reference to a shared resource that can be disposed.
/// </summary>
public interface IAsyncRef
    : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the reference has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the reference has been disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposedException(objectName: "AsyncRef");
        }
    }
}

internal interface IAsyncRef<T>
    : IAsyncRef
    where T : notnull
{
    T Value { get; }
}
