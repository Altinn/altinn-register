using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Register.Configuration;
using Altinn.Register.Extensions;
using Altinn.Register.Services.Interfaces;

using AltinnCore.Authentication.Utils;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace Altinn.Register.Services.Implementation
{
    /// <summary>
    /// App implementation of the authorization service where the app uses the Altinn platform api.
    /// </summary>
    public class AuthorizationClient : IAuthorizationClient
    {
        private readonly GeneralSettings _generalSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _authClient;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationClient"/> class
        /// </summary>
        /// <param name="generalSettings">the general settings</param>
        /// <param name="platformSettings">The platform settings from configuration.</param>
        /// <param name="httpContextAccessor">the http context accessor.</param>
        /// <param name="httpClient">A Http client from the HttpClientFactory.</param>
        /// <param name="logger">the handler for logger service</param>
        public AuthorizationClient(
            IOptions<GeneralSettings> generalSettings,
            IOptions<PlatformSettings> platformSettings,
            IHttpContextAccessor httpContextAccessor,
            HttpClient httpClient,
            ILogger<AuthorizationClient> logger)
        {
            _generalSettings = generalSettings.Value;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            httpClient.BaseAddress = new Uri(platformSettings.Value.ApiAuthorizationEndpoint);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _authClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<bool?> ValidateSelectedParty(int userId, int partyId, CancellationToken cancellationToken = default)
        {
            bool? result;
            string apiUrl = $"parties/{partyId}/validate?userid={userId}";
            string token = JwtTokenUtil.GetTokenFromContext(_httpContextAccessor.HttpContext, _generalSettings.JwtCookieName);

            HttpResponseMessage response = await _authClient.GetAsync(token, apiUrl, cancellationToken: cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string responseData = await response.Content.ReadAsStringAsync(cancellationToken);
                result = JsonConvert.DeserializeObject<bool>(responseData);
            }
            else
            {
                _logger.LogError("Validating selected party {PartyId} for user {UserId} failed with statuscode {StatusCode}", partyId, userId, response.StatusCode);
                result = null;
            }

            return result;
        }
    }
}
