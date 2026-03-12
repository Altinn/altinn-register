namespace Altinn.Register.Core.Mediator;

/// <summary>
/// Marker interface for a request with a response, used by the <see cref="IMediator"/> to identify requests and their corresponding response types.
/// </summary>
/// <typeparam name="TResponse">
///
/// </typeparam>
public interface IRequest<TResponse>
{
}
