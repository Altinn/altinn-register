using System.Collections.Specialized;
using System.Web;
using CommunityToolkit.Diagnostics;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten.Tests.Utils;

internal static class HttpContentFormDataExtensions
{
    private const string ApplicationFormUrlEncoded = "application/x-www-form-urlencoded";

    /// <summary>
    /// Determines whether the specified content is HTML form URL-encoded data, also known as <c>application/x-www-form-urlencoded</c> data.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>
    /// <c>true</c> if the specified content is HTML form URL-encoded data; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsFormData(this HttpContent content)
    {
        Guard.IsNotNull(content);

        var contentType = content.Headers.ContentType;
        return contentType is not null && string.Equals(ApplicationFormUrlEncoded, contentType.MediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a <see cref="Task{T}"/> that will yield a <see cref="NameValueCollection"/> instance containing the form data
    /// parsed as HTML form URL-encoded from the <paramref name="content"/> instance.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{T}"/> which will provide the result. If the data can not be read
    /// as HTML form URL-encoded data then the result is null.</returns>
    public static async Task<NameValueCollection> ReadAsFormDataAsync(this HttpContent content, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(content);

        if (!content.IsFormData())
        {
            // TODO: support multipart?
            throw new InvalidOperationException("Expected form data content");
        }

        var data = await content.ReadAsStringAsync(cancellationToken);
        return HttpUtility.ParseQueryString(data);
    }
}
