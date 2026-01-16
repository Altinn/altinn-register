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
            get => GetValueOrDefault(options, KeyMaskinPortenClientName, defaultValue: null);
            set => SetOrRemoveValue(options, KeyMaskinPortenClientName, value);
        }

        /// <summary>
        /// Gets or sets the Maskinporten access token used for authentication with external services.
        /// </summary>
        internal MaskinPortenToken? MaskinPortenToken
        {
            get => GetValueOrDefault(options, KeyMaskinPortenToken, defaultValue: null);
            set => SetOrRemoveValue(options, KeyMaskinPortenToken, value);
        }

        /// <summary>
        /// Attempts to retrieve the Maskinporten client name.
        /// </summary>
        /// <param name="clientName">When this method returns <see langword="true"/>, contains the Maskinporten client name associated with the
        /// current request; otherwise, contains <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the Maskinporten client name was found; otherwise, <see langword="false"/>.</returns>
        public bool TryGetMaskinPortenClientName([MaybeNullWhen(false)] out string clientName)
            => options.TryGetValue(KeyMaskinPortenClientName, out clientName);

        /// <summary>
        /// Attempts to retrieve the current MaskinPorten token.
        /// </summary>
        /// <param name="token">When this method returns, contains the MaskinPorten token if one is available; otherwise, contains <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if a MaskinPorten token was found; otherwise, <see langword="false"/>.</returns>
        internal bool TryGetMaskinPortenToken([MaybeNullWhen(false)] out MaskinPortenToken token)
            => options.TryGetValue(KeyMaskinPortenToken, out token);
    }

    [return: MaybeNull]
    private static T GetValueOrDefault<T>(HttpRequestOptions options, HttpRequestOptionsKey<T> key, T? defaultValue = default)
    {
        return options.TryGetValue(key, out var value)
            ? value
            : defaultValue;
    }

    private static void SetOrRemoveValue<T>(HttpRequestOptions options, HttpRequestOptionsKey<T> key, T? value)
    {
        if (value is null)
        {
            RemoveKey(options, key);
        }
        else
        {
            options.Set(key, value);
        }
    }

    private static void RemoveKey<T>(IDictionary<string, object?> options, HttpRequestOptionsKey<T> key)
        => options.Remove(key.Key);
}
