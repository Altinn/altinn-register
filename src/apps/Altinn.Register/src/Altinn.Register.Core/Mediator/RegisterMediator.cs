using System.Diagnostics;
using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Core.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Core.Mediator;

/// <summary>
/// A simple mediator implementation that dispatches request for register.
/// </summary>
internal sealed class RegisterMediator
{
    private readonly ApiSourceSwitchProvider _apiSourceSwitchProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterMediator"/> class.
    /// </summary>
    public RegisterMediator(ApiSourceSwitchProvider apiSourceSwitchProvider)
    {
        _apiSourceSwitchProvider = apiSourceSwitchProvider;
    }

    private ValueTask<Result<Contracts.V1.Organization>> Send(GetV1OrganizationRequest request, Sender sender, CancellationToken cancellationToken)
        => RequestDispatcher<GetV1OrganizationRequest, Contracts.V1.Organization>
            .ApiSourceSwitch<GetOrganizationFromA2RequestHandler, GetOrganizationFromDBRequestHandler>(this, sender, "organizations/get")
            .Handle(request, cancellationToken);

    private ValueTask<Result<Contracts.V1.Person>> Send(GetV1PersonRequest request, Sender sender, CancellationToken cancellationToken)
        => RequestDispatcher<GetV1PersonRequest, Contracts.V1.Person>
            .ApiSourceSwitch<GetV1PersonFromA2RequestHandler, GetV1PersonFromDBRequestHandler>(this, sender, "persons/get")
            .Handle(request, cancellationToken);

    private ValueTask<Result<Contracts.V1.Party>> Send(LookupV1PartyRequest request, Sender sender, CancellationToken cancellationToken)
        => RequestDispatcher<LookupV1PartyRequest, Contracts.V1.Party>
            .ApiSourceSwitch<LookupV1PartyFromA2RequestHandler, LookupV1PartyFromDBRequestHandler>(this, sender, "parties/lookup")
            .Handle(request, cancellationToken);

    private ValueTask<Result<Contracts.V1.PartyNamesLookupResult>> Send(LookupV1PartyNamesRequest request, Sender sender, CancellationToken cancellationToken)
        => RequestDispatcher<LookupV1PartyNamesRequest, Contracts.V1.PartyNamesLookupResult>
            .ApiSourceSwitch<LookupV1PartyNamesFromA2RequestHandler, LookupV1PartyNamesFromDBRequestHandler>(this, sender, "parties/nameslookup")
            .Handle(request, cancellationToken);

    private static class RequestDispatcher<TRequest, TResponse>
        where TRequest : notnull, IRequest<TResponse>
        where TResponse : notnull
    {
        public static IRequestHandler<TRequest, TResponse> ApiSourceSwitch<TA2, TDB>(
            RegisterMediator mediator,
            Sender sender,
            string endpointName)
            where TA2 : IRequestHandler<TRequest, TResponse>
            where TDB : IRequestHandler<TRequest, TResponse>
        {
            var source = mediator._apiSourceSwitchProvider.GetSourceForEndpoint(endpointName);
            return source switch
            {
                ApiSource.A2 => sender.Services.GetRequiredService<TA2>(),
                ApiSource.DB => sender.Services.GetRequiredService<TDB>(),
                _ => throw new UnreachableException($"Unsupported API source: {source}"),
            };
        }
    }

    /// <summary>
    /// Sender used by application. This is split from the mediator because the mediator is registered
    /// as a singleton, while the sender is registered as transient to allow it to use scoped services.
    /// </summary>
    internal sealed class Sender
        : IRequestSender<GetV1OrganizationRequest, Contracts.V1.Organization>
        , IRequestSender<GetV1PersonRequest, Contracts.V1.Person>
        , IRequestSender<LookupV1PartyRequest, Contracts.V1.Party>
        , IRequestSender<LookupV1PartyNamesRequest, Contracts.V1.PartyNamesLookupResult>
    {
        private readonly RegisterMediator _mediator;
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="Sender"/> class.
        /// </summary>
        public Sender(RegisterMediator mediator, IServiceProvider services)
        {
            _mediator = mediator;
            _services = services;
        }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/>.
        /// </summary>
        internal IServiceProvider Services => _services;

        /// <inheritdoc/>
        public ValueTask<Result<Contracts.V1.Organization>> Send(
            GetV1OrganizationRequest request,
            CancellationToken cancellationToken = default)
            => _mediator.Send(request, this, cancellationToken);

        /// <inheritdoc/>
        public ValueTask<Result<Contracts.V1.Person>> Send(
            GetV1PersonRequest request,
            CancellationToken cancellationToken = default)
            => _mediator.Send(request, this, cancellationToken);

        /// <inheritdoc/>
        public ValueTask<Result<Contracts.V1.Party>> Send(
            LookupV1PartyRequest request,
            CancellationToken cancellationToken = default)
            => _mediator.Send(request, this, cancellationToken);

        /// <inheritdoc/>
        public ValueTask<Result<Contracts.V1.PartyNamesLookupResult>> Send(
            LookupV1PartyNamesRequest request,
            CancellationToken cancellationToken = default)
            => _mediator.Send(request, this, cancellationToken);
    }
}
