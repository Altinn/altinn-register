using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

namespace System.Net.Http;

/// <summary>
/// Extensions for <see cref="HttpRequestMessage"/>.
/// </summary>
public static class MaskinPortenHttpRequestMessageExtensions
{
    private static readonly HttpRequestOptionsKey<string> KeyMaskinPortenClientName = new($"{nameof(Altinn.Authorization.ServiceDefaults)}.{nameof(MaskinPortenClientOptions)}:name");
    private static readonly HttpRequestOptionsKey<MaskinPortenToken> KeyMaskinPortenToken = new($"{nameof(Altinn.Authorization.ServiceDefaults)}.{nameof(MaskinPortenToken)}");

    /// <param name="options">The <see cref="HttpRequestOptions"/>.</param>
    extension(HttpRequestOptions options)
    {
        /// <summary>
        /// Gets or sets the name of the MaskinPorten client to use to authenticate this request.
        /// </summary>
        public string? MaskinPortenClientName
        {
            get => GetMaskinPortenClientName(options);
            set => SetMaskinPortenClientName(options, value);
        }

        internal bool TryGetMaskinPortenClientName([MaybeNullWhen(false)] out string clientName)
            => options.TryGetValue(KeyMaskinPortenClientName, out clientName);
    }

    private static string? GetMaskinPortenClientName(HttpRequestOptions options)
        => options.TryGetMaskinPortenClientName(out var clientName)
        ? clientName
        : null;

    private static void SetMaskinPortenClientName(HttpRequestOptions options, string? clientName)
    {
        if (clientName is null)
        {
            RemoveMaskinPortenClientName(options);
        }
        else
        {
            options.Set(KeyMaskinPortenClientName, clientName);
        }

        static void RemoveMaskinPortenClientName(IDictionary<string, object?> options)
            => options.Remove(KeyMaskinPortenClientName.Key);
    }
}
