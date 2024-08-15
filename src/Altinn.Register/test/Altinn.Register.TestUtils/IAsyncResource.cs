namespace Altinn.Register.TestUtils;

/// <summary>
/// Represents a resource that is created and destroyed asynchronously.
/// </summary>
/// <typeparam name="TSelf">The self type.</typeparam>
public interface IAsyncResource<TSelf>
    : IAsyncDisposable
    where TSelf : IAsyncResource<TSelf>
{
    /// <summary>
    /// Creates a new instance of the resource.
    /// </summary>
    /// <returns>The resource.</returns>
    public abstract static ValueTask<TSelf> New();
}
