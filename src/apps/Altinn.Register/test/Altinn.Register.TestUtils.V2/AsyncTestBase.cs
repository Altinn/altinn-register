using Xunit;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Base class for tests that needs async resources.
/// </summary>
public abstract class AsyncTestBase
    : IAsyncLifetime
    , IAsyncDisposable
{
    Task IAsyncLifetime.DisposeAsync()
        => DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync()
        => DisposeAsync();

    Task IAsyncLifetime.InitializeAsync()
        => InitializeAsync().AsTask();

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync()"/>
    protected virtual ValueTask DisposeAsync() 
        => ValueTask.CompletedTask;

    /// <inheritdoc cref="IAsyncLifetime.InitializeAsync()"/>
    protected virtual ValueTask InitializeAsync()
        => ValueTask.CompletedTask;
}
