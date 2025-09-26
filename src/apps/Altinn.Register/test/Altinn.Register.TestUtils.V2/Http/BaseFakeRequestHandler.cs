using CommunityToolkit.Diagnostics;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// Base class for fake request handlers.
/// </summary>
public abstract class BaseFakeRequestHandler
    : IFakeRequestHandler
    , ISetFakeRequestHandler
{
    private FakeRequestDelegate _handler;

    internal BaseFakeRequestHandler(FakeRequestDelegate? handler = null)
    {
        _handler = handler ?? ((_, _) => ThrowHelper.ThrowInvalidOperationException<Task>($"Handler not configured: {Description}"));
    }

    /// <inheritdoc cref="IFakeRequestHandler.Description"/>.
    protected abstract string Description { get; }

    string IFakeRequestHandler.Description
        => Description;

    /// <inheritdoc cref="IFakeRequestHandler.CanHandle(FakeRequestContext)"/>
    protected abstract bool CanHandle(FakeRequestContext context);

    /// <inheritdoc/>
    bool IFakeRequestHandler.CanHandle(FakeRequestContext context)
        => CanHandle(context);

    /// <inheritdoc/>
    Task IFakeRequestHandler.Handle(FakeRequestContext context, CancellationToken cancellationToken)
        => _handler(context, cancellationToken);

    /// <inheritdoc/>
    void ISetFakeRequestHandler.SetHandler(FakeRequestDelegate handler)
        => _handler = handler;
}
