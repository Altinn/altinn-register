using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.TestUtils;

internal interface IAsyncRef
    : IAsyncDisposable
{
    bool IsDisposed { get; }

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
