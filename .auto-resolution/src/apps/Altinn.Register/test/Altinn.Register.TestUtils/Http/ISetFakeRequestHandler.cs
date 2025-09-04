namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// Interface used to set a delegate that handles requests for this <see cref="IFakeRequestHandler"/>.
/// </summary>
public interface ISetFakeRequestHandler
{
    /// <summary>
    /// Sets a delegate that handles requests for this <see cref="IFakeRequestHandler"/>.
    /// </summary>
    /// <param name="handler">The handler delegate.</param>
    void SetHandler(FakeRequestDelegate handler);
}

/// <summary>
/// Represents a delegate that handles a request.
/// </summary>
/// <param name="context">The request context.</param>
/// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
public delegate Task FakeRequestDelegate(FakeRequestContext context, CancellationToken cancellationToken);
