using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Altinn.Register.TestUtils.Http.Filters;

internal sealed class RouteFilter
    : IFakeRequestFilter
{
    public static IFakeRequestFilter Create([StringSyntax("Route")] string routeTemplate)
        => Create(TemplateParser.Parse(routeTemplate));

    public static IFakeRequestFilter Create(RouteTemplate routeTemplate)
        => new RouteFilter(routeTemplate);

    private readonly TemplateMatcher _matcher;

    private RouteFilter(RouteTemplate template)
    {
        _matcher = new(template, new());
    }

    public string Description => $"has path '{_matcher.Template.TemplateText}'";

    public bool Matches(FakeHttpRequestMessage request)
    {
        if (request.RequestUri is null)
        {
            return false;
        }

        var baseUrl = FakeHttpMessageHandler.FakeBasePath;
        var relativeUrl = baseUrl.MakeRelativeUri(request.RequestUri);
        if (relativeUrl.IsAbsoluteUri)
        {
            return false;
        }

        var relStr = $"/{relativeUrl}";
        if (relStr.StartsWith("/../"))
        {
            return false;
        }

        if (relStr.IndexOf('?') is var queryStart and >= 0)
        {
            relStr = relStr[..queryStart];
        }

        var values = new RouteValueDictionary();
        if (!_matcher.TryMatch(relStr, values))
        {
            return false;
        }

        request.RouteData = new RouteData(values);
        return true;
    }
}
