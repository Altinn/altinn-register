namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// Interface used to add filters to a <see cref="IFakeRequestHandler"/>.
/// </summary>
public interface IFilterFakeRequest
{
    /// <summary>
    /// Adds a filter to the <see cref="IFakeRequestHandler"/>.
    /// </summary>
    /// <param name="filter">The filter.</param>
    void AddFilter(IFakeRequestFilter filter);
}
