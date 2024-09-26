using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Altinn.Register.Tests.Mocks;

public class MockMessageHandler
{
    private static readonly HttpRequestOptionsKey<RouteData> RouteDataKey 
        = new($"{nameof(MockMessageHandler)}.{nameof(RouteData)}");

    private readonly Uri _baseAddress;

    private readonly ImmutableArray<RequestHandler>.Builder _handlers 
        = ImmutableArray.CreateBuilder<RequestHandler>();

    public MockMessageHandler(string baseAddress)
    {
        _baseAddress = new(baseAddress, UriKind.Absolute);
    }

    public HttpMessageHandler Create(IServiceProvider services)
        => new Handler(_handlers.ToImmutable(), services);

    public MockMessageHandler AddHandler(
        Predicate<HttpRequestMessage> predicate,
        Func<HttpRequestMessage, IServiceProvider, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handlers.Add(new(predicate, handler));

        return this;
    }

    public MockMessageHandler AddHandler(
        Predicate<HttpRequestMessage> predicate,
        Func<HttpRequestMessage, HttpResponseMessage> handler)
        => AddHandler(predicate, (request, _, cancellationToken) => Task.FromResult(handler(request)));

    public MockMessageHandler MapGet([StringSyntax("Route")] string routeTemplate, Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var predicate = CreateRoutePredicate([HttpMethod.Get], routeTemplate, _baseAddress);

        return AddHandler(predicate, handler);
    }

    private static Predicate<HttpRequestMessage> CreateRoutePredicate(
        ImmutableArray<HttpMethod> methods, 
        [StringSyntax("Route")] string routeTemplate,
        Uri baseAddress)
    {
        var matcher = new TemplateMatcher(TemplateParser.Parse(routeTemplate), new());

        return (HttpRequestMessage request) =>
        {
            if (!methods.Contains(request.Method))
            {
                return false;
            }

            if (request.RequestUri is not { } uri)
            {
                return false;
            }

            var relative = baseAddress.MakeRelativeUri(uri);
            if (relative.IsAbsoluteUri)
            {
                return false;
            }

            var relStr = $"/{relative}";
            if (relStr.StartsWith("/../"))
            {
                return false;
            }

            var values = new RouteValueDictionary();
            if (!matcher.TryMatch(relStr, values))
            {
                return false;
            }

            request.Options.Set(RouteDataKey, new RouteData(values));
            return true;
        };
    }

    private sealed class RequestHandler(
        Predicate<HttpRequestMessage> predicate, 
        Func<HttpRequestMessage, IServiceProvider, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        private readonly Predicate<HttpRequestMessage> _predicate = predicate;
        private readonly Func<HttpRequestMessage, IServiceProvider, CancellationToken, Task<HttpResponseMessage>> _handler = handler;

        public bool CanHandle(HttpRequestMessage request) 
            => _predicate(request);

        public Task<HttpResponseMessage> Handle(HttpRequestMessage request, IServiceProvider services, CancellationToken cancellationToken) 
            => _handler(request, services, cancellationToken);
    }

    private sealed class Handler
        : HttpMessageHandler
    {
        private readonly ImmutableArray<RequestHandler> _handlers;
        private readonly IServiceProvider _serviceProvider;

        public Handler(ImmutableArray<RequestHandler> handlers, IServiceProvider serviceProvider)
        {
            _handlers = handlers;
            _serviceProvider = serviceProvider;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(request))
                {
                    return handler.Handle(request, _serviceProvider, cancellationToken);
                }
            }

            throw new InvalidOperationException($"Request to {request.RequestUri} not handled");
        }
    }
}
