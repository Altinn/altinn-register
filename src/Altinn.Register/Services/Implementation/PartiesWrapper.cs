using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Configuration;
using Altinn.Register.Services.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Services.Implementation;

/// <summary>
/// The parties wrapper
/// </summary>
public class PartiesWrapper : IParties
{
    private readonly GeneralSettings _generalSettings;
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private readonly IMemoryCache _memoryCache;
    private static readonly SemaphoreSlim _concurrentNameLookupsLimiter = new(20);
    private const int _cacheTimeout = 5;
    private const int _cacheTimeoutForPartyNames = 360;
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
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
            party = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync(), options);
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
            Party party = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync(), options);
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
    public async Task<List<Party>> GetPartyList(List<int> partyIds, bool fetchSubUnits = false)
    {
        UriBuilder uriBuilder = new UriBuilder($"{_generalSettings.BridgeApiEndpoint}parties?fetchSubUnits={fetchSubUnits}");

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

    /// <inheritdoc />
    public async Task<Party> GetPartyByUuid(Guid partyUuid)
    {
        string cacheKey = $"PartyUUID:{partyUuid}";
        if (_memoryCache.TryGetValue(cacheKey, out Party party))
        {
            return party;
        }

        Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}parties?partyuuid={partyUuid}");

        HttpResponseMessage response = await _client.GetAsync(endpointUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            party = await JsonSerializer.DeserializeAsync<Party>(await response.Content.ReadAsStreamAsync(), options);
            _memoryCache.Set(cacheKey, party, new TimeSpan(0, _cacheTimeout, 0));
            return party;
        }
        
        _logger.LogError("Getting party with party Id {PartyUuid} failed with statuscode {StatusCode}", partyUuid, response.StatusCode);
        return null;
    }

    /// <inheritdoc />
    public async Task<List<Party>> GetPartyListByUuid(List<Guid> partyUuids, bool fetchSubUnits = false)
    {
        UriBuilder uriBuilder = new UriBuilder($"{_generalSettings.BridgeApiEndpoint}parties/byuuid?fetchSubUnits={fetchSubUnits}");

        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyUuids), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _client.PostAsync(uriBuilder.Uri, requestBody);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            string responseContent = await response.Content.ReadAsStringAsync();
            List<Party> partiesInfo = JsonSerializer.Deserialize<List<Party>>(responseContent, options);
            return partiesInfo;
        }
        
        _logger.LogError("Getting parties information from bridge failed with {StatusCode}", response.StatusCode);
        return new List<Party>();
    }

    /// <inheritdoc />
    public async Task<PartyNamesLookupResult> LookupPartyNames(PartyNamesLookup partyNamesLookup)
    {
        var partyNames = new ConcurrentBag<PartyName>();
        var tasks = new List<Task>();

        foreach (var partyLookup in partyNamesLookup.Parties)
        {
            // The static semaphore is used to limit the number of concurrent name lookups
            // application wide (ie. per pod) to control load on the bridge API.
            // ProcessPartyLookupAsync will release the semaphore when it's done,
            // freeing up a slot for the next lookup.
            await _concurrentNameLookupsLimiter.WaitAsync();
            tasks.Add(ProcessPartyLookupAsync(partyLookup, partyNames));
        }

        await Task.WhenAll(tasks);

        return new PartyNamesLookupResult
        {
            PartyNames = partyNames.ToList()
        };
    }

    private async Task ProcessPartyLookupAsync(PartyLookup partyLookup, ConcurrentBag<PartyName> partyNames)
    {
        try
        {
            string lookupValue = !string.IsNullOrEmpty(partyLookup.Ssn) ? partyLookup.Ssn : partyLookup.OrgNo;
            string cacheKey = $"n:{lookupValue}";
            string partyName = await GetOrAddPartyNameToCacheAsync(lookupValue, cacheKey);

            partyNames.Add(new PartyName
            {
                Ssn = partyLookup.Ssn,
                OrgNo = partyLookup.OrgNo,
                Name = partyName
            });
        }
        finally
        {
            _concurrentNameLookupsLimiter.Release();
        }
    }

    private async Task<string> GetOrAddPartyNameToCacheAsync(string lookupValue, string cacheKey)
    {
        if (_memoryCache.TryGetValue(cacheKey, out string partyName))
        {
            return partyName;
        }

        Party party = await LookupPartyBySSNOrOrgNo(lookupValue);

        if (party != null)
        {
            partyName = party.Name;
            _memoryCache.Set(cacheKey, party.Name, new TimeSpan(0, _cacheTimeoutForPartyNames, 0));
        }

        return partyName;
    }
}
