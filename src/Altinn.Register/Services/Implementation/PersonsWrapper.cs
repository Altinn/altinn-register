#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Configuration;
using Altinn.Register.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Services.Implementation;

/// <summary>
/// The persons wrapper
/// </summary>
public class PersonsWrapper : IPersons
{
    private readonly GeneralSettings _generalSettings;
    private readonly ILogger _logger;
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonsWrapper"/> class
    /// </summary>
    /// <param name="httpClient">HttpClient from default httpclientfactory</param>
    /// <param name="generalSettings">The general settings</param>
    /// <param name="logger">The logger</param>
    public PersonsWrapper(HttpClient httpClient, IOptions<GeneralSettings> generalSettings, ILogger<PersonsWrapper> logger)
    {
        _generalSettings = generalSettings.Value;
        _logger = logger;
        _client = httpClient;
    }

    /// <inheritdoc />
    public async Task<Person?> GetPerson(string nationalIdentityNumber)
    {
        Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}persons");

        StringContent requestBody = new(JsonSerializer.Serialize(nationalIdentityNumber), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return await JsonSerializer.DeserializeAsync<Person>(await response.Content.ReadAsStreamAsync());
        }
        else
        {
            _logger.LogError("Getting person failed with statuscode {StatusCode}", response.StatusCode);
        }

        return null;
    }
}
