using System.Net.Http.Headers;
using System.Text;
using Altinn.Register.TestUtils.Http.Filters;
using Altinn.Register.TestUtils.Http.Handlers;
using Microsoft.AspNetCore.Http;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// Extension methods for <see cref="FakeHttpMessageHandler"/>.
/// </summary>
public static class FakeHttpExtensions
{
    /// <summary>
    /// Adds an expectation for a request with the specified method and path.
    /// </summary>
    /// <param name="handler">The <see cref="FakeHttpMessageHandler"/>.</param>
    /// <param name="method">The <see cref="HttpMethod"/>.</param>
    /// <param name="route">The request path.</param>
    /// <returns>A <see cref="IFakeRequestBuilder"/> to continue building the expectation.</returns>
    public static IFakeRequestBuilder Expect(this FakeHttpMessageHandler handler, HttpMethod method, PathString route)
    {
        var requestHandler = new FakeRequestHandler();
        handler.Expect(requestHandler);

        IFakeRequestBuilder builder = requestHandler;
        builder.AddFilter(HttpMethodFilter.Get(method));
        builder.AddFilter(RouteFilter.Create(route));

        return builder;
    }

    /// <summary>
    /// Adds a query filter to the request.
    /// </summary>
    /// <typeparam name="T">The request builder type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="queryParam">The query param.</param>
    /// <param name="value">The expected value.</param>
    /// <returns><paramref name="builder"/>.</returns>
    public static T WithQuery<T>(this T builder, string queryParam, string value)
        where T : IFilterFakeRequest
    {
        builder.AddFilter(QueryParamFilter.Create(queryParam, value));
        
        return builder;
    }

    /// <summary>
    /// Sets the response for the request.
    /// </summary>
    /// <typeparam name="T">The request builder type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="handler">The response delegate.</param>
    public static void Respond<T>(this T builder, Delegate handler)
        where T : ISetFakeRequestHandler
    {
        var factory = FakeRequestDelegateFactory.Create(handler);

        builder.SetHandler(factory);
    }

    /// <summary>
    /// Sets the response for the request.
    /// </summary>
    /// <typeparam name="T">The request builder type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="content">The response content.</param>
    public static void Respond<T>(this T builder, HttpContent content)
        where T : ISetFakeRequestHandler
        => Respond(builder, () => content);

    /// <summary>
    /// Sets the response for the request.
    /// </summary>
    /// <typeparam name="T">The request builder type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="contentType">The response content type.</param>
    /// <param name="content">The response content.</param>
    public static void Respond<T>(this T builder, string contentType, string content)
        where T : ISetFakeRequestHandler
        => Respond(builder, () => new StringContent(content, Encoding.UTF8, new MediaTypeHeaderValue(contentType, "utf-8")));
}
