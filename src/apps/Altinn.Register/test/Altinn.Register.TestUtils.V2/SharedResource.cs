using CommunityToolkit.Diagnostics;
using Xunit;

namespace Altinn.Register.TestUtils;

/// <summary>
/// An asynchronous reference to a shared resource that can be disposed.
/// </summary>
public abstract class SharedResource
    : IAsyncLifetime
{
    private IAsyncRef? _ref;

    /// <summary>
    /// Gets a reference to the shared resource.
    /// </summary>
    /// <returns>A <see cref="IAsyncRef"/>.</returns>
    protected abstract Task<IAsyncRef> GetRef();

    Task IAsyncLifetime.DisposeAsync()
    {
        if (_ref is { } r)
        {
            return r.DisposeAsync().AsTask();
        }

        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        Guard.IsNull(_ref);

        var r = await GetRef();

        try
        {
            r = Interlocked.Exchange(ref _ref, r);

            if (r is not null)
            {
                ThrowHelper.ThrowInvalidOperationException("Resource already initialized");
            }
        }
        finally
        {
            if (r is not null)
            {
                await r.DisposeAsync();
            }
        }
    }
}
