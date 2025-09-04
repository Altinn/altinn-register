namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// A handler for requests.
/// </summary>
public interface IFakeRequestHandler
{
    /// <summary>
    /// Gets a human readable description of the handler.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets a value indicating whether this handler can handle the request.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <returns><see langword="true"/>, if this handler can handle <paramref name="context"/>, otherwise <see langword="false"/>.</returns>
    bool CanHandle(FakeRequestContext context);

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    Task Handle(FakeRequestContext context, CancellationToken cancellationToken);
}
