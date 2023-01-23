using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Configuration;
using Altinn.Register.Services.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Services.Implementation
{
    /// <summary>
    /// The parties wrapper
    /// </summary>
    public class PartiesWrapper : IParties
    {
        private readonly GeneralSettings _generalSettings;
        private readonly ILogger _logger;
        private readonly HttpClient _client;
        private readonly IMemoryCache _memoryCache;
        private const int _cacheTimeout = 5;
        private readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="PartiesWrapper"/> class
        /// </summary>
        /// <param name="httpClient">HttpClient from default httpclientfactory</param>
        /// <param name="generalSettings">the general settings</param>
        /// <param name="logger">the logger</param>
        /// <param name="memoryCache">the memory cache</param>
        public PartiesWrapper(HttpClient httpClient, IOptions<GeneralSettings> generalSettings, ILogger<PartiesWrapper> logger, IMemoryCache memoryCache)
        {
            _generalSettings = generalSettings.Value;
            _logger = logger;
            _client = httpClient;
            _memoryCache = memoryCache;
        }

        /// <inheritdoc />
        public async Task<Party> GetParty(int partyId)
        {
            string cacheKey = $"PartyId:{partyId}";
            if (_memoryCache.TryGetValue(cacheKey, out Party party))
            {
                return party;
            }

            Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}parties/{partyId}");

            HttpResponseMessage response = await _client.GetAsync(endpointUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                party = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync());
                _memoryCache.Set(cacheKey, party, new TimeSpan(0, _cacheTimeout, 0));
                return party;
            }
            else
            {
                _logger.LogError("Getting party with party Id {PartyId} failed with statuscode {StatusCode}", partyId, response.StatusCode);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<Party> LookupPartyBySSNOrOrgNo(string lookupValue)
        {
            Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}parties/lookupObject");

            StringContent requestBody = new(JsonSerializer.Serialize(lookupValue), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                Party party = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync());
                _memoryCache.Set($"PartyId:{party.PartyId}", party, new TimeSpan(0, _cacheTimeout, 0));
                return party;
            }
            else
            {
                _logger.LogError("Getting party by lookup value failed with statuscode {StatusCode}", response.StatusCode);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<int> LookupPartyIdBySSNOrOrgNo(string lookupValue)
        {
            Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}parties/lookup");

            StringContent requestBody = new(JsonSerializer.Serialize(lookupValue), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return await JsonSerializer.DeserializeAsync<int>(await response.Content.ReadAsStreamAsync());
            }
            else
            {
                _logger.LogError("Getting party id by lookup value failed with statuscode {StatusCode}", response.StatusCode);
            }

            return -1;
        }

        /// <inheritdoc />
        public async Task<List<Party>> GetPartyList(List<int> partyIds)
        {
            UriBuilder uriBuilder = new UriBuilder($"{_generalSettings.BridgeApiEndpoint}parties");

            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _client.PostAsync(uriBuilder.Uri, requestBody);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                List<Party> partiesInfo = JsonSerializer.Deserialize<List<Party>>(responseContent, options);
                return partiesInfo;
            }
            else
            {
                _logger.LogError("Getting parties information from bridge failed with {StatusCode}", response.StatusCode);
            }

            return null;
        }
    }
}
