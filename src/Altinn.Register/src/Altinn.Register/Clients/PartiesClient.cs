#nullable enable

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Altinn.Platform.Register.Models;
using Altinn.Register.Configuration;
using Altinn.Register.Core.Parties;
using Altinn.Register.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Clients;

/// <summary>
/// Implementation of <see cref="IPartyClient"/> using SBL Bridge Register API as data source
/// </summary>
public class PartiesClient : IPartyClient
{
    // TODO: This should be moved into the http client, so that it works for all calls
    private static readonly SemaphoreSlim _concurrentNameLookupsLimiter = new(20);

    private readonly static JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly GeneralSettings _generalSettings;
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private readonly IMemoryCache _memoryCache;
    private const int _cacheTimeout = 5;
    private const int _cacheTimeoutForPartyNames = 360;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartiesClient"/> class
    /// </summary>
    public PartiesClient(HttpClient httpClient, IOptions<GeneralSettings> generalSettings, ILogger<PartiesClient> logger, IMemoryCache memoryCache)
    {
        _generalSettings = generalSettings.Value;
        _logger = logger;
        _client = httpClient;
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<Party?> GetPartyById(int partyId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartyId:{partyId}";
        if (_memoryCache.TryGetValue(cacheKey, out Party? party))
        {
            return party;
        }

        Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}parties/{partyId}");

        HttpResponseMessage response = await _client.GetAsync(endpointUrl, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            party = await response.Content.ReadFromJsonAsync<Party>(JsonOptions, cancellationToken);
            if (party is null)
            {
                return null;
            }

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
    public async Task<Party?> GetPartyById(Guid partyUuid, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"PartyUUID:{partyUuid}";
        if (_memoryCache.TryGetValue(cacheKey, out Party? party))
        {
            return party;
        }

        Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}parties?partyuuid={partyUuid}");

        HttpResponseMessage response = await _client.GetAsync(endpointUrl, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            party = await response.Content.ReadFromJsonAsync<Party>(JsonOptions, cancellationToken);
            if (party is null)
            {
                return null;
            }

            _memoryCache.Set(cacheKey, party, new TimeSpan(0, _cacheTimeout, 0));
            return party;
        }

        _logger.LogError("Getting party with party Id {PartyUuid} failed with statuscode {StatusCode}", partyUuid, response.StatusCode);
        return null;
    }

    /// <inheritdoc />
    public async Task<Party?> LookupPartyBySSNOrOrgNo(string lookupValue, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"LookupValue:{lookupValue}";
        if (_memoryCache.TryGetValue(cacheKey, out Party? party))
        {
            return party;
        }

        Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}parties/lookupObject");

        StringContent requestBody = new(JsonSerializer.Serialize(lookupValue), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            party = await response.Content.ReadFromJsonAsync<Party>(JsonOptions, cancellationToken);

            if (party is null)
            {
                return null;
            }

            _memoryCache.Set(cacheKey, party, new TimeSpan(0, _cacheTimeout, 0));
            return party;
        }
        else
        {
            _logger.LogError("Getting party by lookup value failed with statuscode {StatusCode}", response.StatusCode);
        }

        return null;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Party> GetPartiesById(IEnumerable<int> partyIds, bool fetchSubUnits, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UriBuilder uriBuilder = new UriBuilder($"{_generalSettings.BridgeApiEndpoint}parties?fetchSubUnits={fetchSubUnits}");

        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _client.PostAsync(uriBuilder.Uri, requestBody, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            await foreach (var party in response.Content.ReadFromJsonAsAsyncEnumerable<Party>(JsonOptions, cancellationToken))
            {
                if (party is not null)
                {
                    yield return party;
                }
            }
        }
        else
        {
            _logger.LogError("Getting parties information from bridge failed with {StatusCode}", response.StatusCode);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<Party> GetPartiesById(IEnumerable<int> partyIds, CancellationToken cancellationToken = default)
        => GetPartiesById(partyIds, fetchSubUnits: false, cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<Party> GetPartiesById(IEnumerable<Guid> partyIds, bool fetchSubUnits, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UriBuilder uriBuilder = new UriBuilder($"{_generalSettings.BridgeApiEndpoint}parties/byuuid?fetchSubUnits={fetchSubUnits}");

        StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _client.PostAsync(uriBuilder.Uri, requestBody, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            await foreach (var party in response.Content.ReadFromJsonAsAsyncEnumerable<Party>(JsonOptions, cancellationToken))
            {
                if (party is not null)
                {
                    yield return party;
                }
            }

            yield break;
        }

        _logger.LogError("Getting parties information from bridge failed with {StatusCode}", response.StatusCode);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<Party> GetPartiesById(IEnumerable<Guid> partyIds, CancellationToken cancellationToken = default)
        => GetPartiesById(partyIds, fetchSubUnits: false, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<PartyName> LookupPartyNames(IEnumerable<PartyLookup> lookupValues, CancellationToken cancellationToken = default)
    {
        return RunInParallel(lookupValues, ProcessPartyLookupAsync, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Party> LookupPartiesBySSNOrOrgNos(
        IEnumerable<string> lookupValues, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var uri = $"{_generalSettings.BridgeApiEndpoint}parties/byssnorgnumber";

        JsonContent requestBody = JsonContent.Create(lookupValues, options: JsonOptions);
        HttpResponseMessage response = await _client.PostAsync(uri, requestBody, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            await foreach (var party in response.Content.ReadFromJsonAsAsyncEnumerable<Party>(JsonOptions, cancellationToken))
            {
                if (party is not null)
                {
                    yield return party;
                }
            }

            yield break;
        }

        _logger.LogError("Getting parties information from bridge failed with {StatusCode}", response.StatusCode);
    }

    private async Task<PartyName> ProcessPartyLookupAsync(PartyLookup partyLookup, CancellationToken cancellationToken)
    {
        Debug.Assert(!string.IsNullOrEmpty(partyLookup.Ssn) || !string.IsNullOrEmpty(partyLookup.OrgNo));
        string lookupValue = !string.IsNullOrEmpty(partyLookup.Ssn) ? partyLookup.Ssn : partyLookup.OrgNo!;
        string cacheKey = $"n:{lookupValue}";
        string? partyName = await GetOrAddPartyNameToCacheAsync(lookupValue, cacheKey, cancellationToken);

        return new PartyName
        {
            Ssn = partyLookup.Ssn,
            OrgNo = partyLookup.OrgNo,
            Name = partyName,
        };
    }

    private async Task<string?> GetOrAddPartyNameToCacheAsync(string lookupValue, string cacheKey, CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(cacheKey, out string? partyName)
            && partyName is not null)
        {
            return partyName;
        }

        // limit the concurrent calls to SBL Bridge
        await _concurrentNameLookupsLimiter.WaitAsync(cancellationToken);

        Party? party;
        try
        {
            party = await LookupPartyBySSNOrOrgNo(lookupValue, cancellationToken);
        }
        finally
        {
            _concurrentNameLookupsLimiter.Release();
        }

        if (party != null)
        {
            partyName = party.Name;
            _memoryCache.Set(cacheKey, party.Name, new TimeSpan(0, _cacheTimeoutForPartyNames, 0));
        }

        return partyName;
    }

    private static async IAsyncEnumerable<TResult> RunInParallel<TIn, TResult>(
        IEnumerable<TIn> input,
        Func<TIn, CancellationToken, Task<TResult>> func,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tasks = input.Select(i => func(i, cancellationToken)).ToList();

        while (tasks.Count > 0)
        {
            Task<TResult> finishedTask = await Task.WhenAny(tasks);
            tasks.SwapRemove(finishedTask);
            yield return await finishedTask;
        }
    }
}
