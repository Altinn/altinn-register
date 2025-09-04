using System.Diagnostics;

namespace Altinn.Register.IntegrationTests.Tracing;

internal class TracingHandler(string? prefix = null)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var activityVerb = request.Method.ToString().ToLowerInvariant();
        var relPath = request.RequestUri?.PathAndQuery switch
        {
            null => [],
            var path => path.AsSpan(),
        };

        if (!string.IsNullOrEmpty(prefix) && relPath.StartsWith(prefix))
        {
            relPath = relPath[prefix.Length..];
        }

        using var activity = IntegrationTestsActivities.Source.StartActivity(ActivityKind.Client, name: $"{activityVerb} {relPath}");
        if (activity is not null)
        {
            request.Headers.Add("traceparent", activity.Id);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
