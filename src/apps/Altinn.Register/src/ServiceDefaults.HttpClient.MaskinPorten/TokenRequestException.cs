using System.Net;
using System.Text;

namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Represents an exception that is thrown when a token request to MaskinPorten fails.
/// </summary>
/// <param name="message">The error message that describes the reason for the exception.</param>
/// <param name="inner">The exception that is the cause of this exception, or <see langword="null"/> if no inner exception is specified.</param>
/// <param name="statusCode">The HTTP status code returned by the failed token request, or <see langword="null"/> if not available.</param>
public sealed class TokenRequestException(string message, Exception? inner, HttpStatusCode? statusCode)
    : HttpRequestException(message, inner, statusCode)
{
    /// <summary>
    /// Initializes a new <see cref="TokenRequestException"/>.
    /// </summary>
    /// <param name="key">The client key associated with the MaskinPorten request that caused the exception.</param>
    /// <param name="inner">The exception that is the cause of this exception, or <see langword="null"/> if no inner exception is specified.</param>
    /// <param name="statusCode">The HTTP status code returned by the failed token request, or <see langword="null"/> if not available.</param>
    /// <param name="error">A tuple containing the error code and description returned by the token endpoint, or <see langword="null"/> if not provided.</param>
    internal TokenRequestException(MaskinPortenCacheKey key, Exception? inner, HttpStatusCode? statusCode, (string? Error, string? Description)? error)
        : this(CreateMessage(key, statusCode, error), inner, statusCode)
    {
    }

    private static string CreateMessage(
        MaskinPortenCacheKey key,
        HttpStatusCode? statusCode, 
        (string? Error, string? Description)? error)
    {
        var builder = new StringBuilder("Failed to get token from Maskinporten.");
        builder.Append($" ClientId: '{key.ClientId}'; Scope: '{key.Scope}';");
        
        if (statusCode is not null)
        {
            builder.Append($" StatusCode: {(int)statusCode} '{statusCode}';");
        }

        if (error is { Error: var eMsg, Description: var eDesc })
        {
            if (!string.IsNullOrEmpty(eMsg))
            {
                builder.Append($" Error: {eMsg};");
            }

            if (!string.IsNullOrEmpty(eDesc))
            {
                builder.AppendLine().Append($"Error description: {eDesc}");
            }
        }

        return builder.ToString();
    }
}
