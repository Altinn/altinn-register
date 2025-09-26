namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// A request filter.
/// </summary>
public interface IFakeRequestFilter
{
    /// <summary>
    /// A human readable description of the filter. Used in exception messages when a request does not match any filter.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Determines whether the request matches the filter.
    /// </summary>
    /// <param name="request">The request.</param>
    bool Matches(FakeHttpRequestMessage request);
}
