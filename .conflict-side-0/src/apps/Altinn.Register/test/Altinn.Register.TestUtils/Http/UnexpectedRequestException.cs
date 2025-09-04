namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// An exception that's thrown if an unexpected request is received.
/// </summary>
public class UnexpectedRequestException
    : InvalidOperationException
{
    private readonly FakeRequestContext _context;
    private readonly IFakeRequestHandler _expectation;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnexpectedRequestException"/> class.
    /// </summary>
    /// <param name="context">The <see cref="FakeRequestContext"/>.</param>
    /// <param name="expectation">The expected <see cref="IFakeRequestHandler"/>.</param>
    public UnexpectedRequestException(FakeRequestContext context, IFakeRequestHandler expectation)
        : base(CreateMessage(context, expectation))
    {
        _context = context;
        _expectation = expectation;
    }

    /// <summary>
    /// The <see cref="FakeRequestContext"/> that was received.
    /// </summary>
    public FakeRequestContext Context
        => _context;

    /// <summary>
    /// The expected <see cref="IFakeRequestHandler"/> that could not handle the request.
    /// </summary>
    public IFakeRequestHandler Expectation
        => _expectation;

    private static string CreateMessage(FakeRequestContext context, IFakeRequestHandler expectation)
    {
        return $"""
            Unexpected request: {context.Request.Method} {context.Request.RequestUri}.
            Expected: {expectation.Description}
            """;
    }
}
