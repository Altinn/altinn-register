namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// Combination of <see cref="IFilterFakeRequest"/> and <see cref="ISetFakeRequestHandler"/>.
/// </summary>
public interface IFakeRequestBuilder
    : IFilterFakeRequest
    , ISetFakeRequestHandler
{
}
