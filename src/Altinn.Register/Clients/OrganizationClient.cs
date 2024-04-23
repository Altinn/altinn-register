using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Configuration;
using Altinn.Register.Exceptions;
using Altinn.Register.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Clients
{
    /// <summary>
    /// Implementation of <see cref="IOrganizationClient"/> using SBL Bridge Register API as data source
    /// </summary>
    public class OrganizationClient : IOrganizationClient
    {
        private readonly GeneralSettings _generalSettings;
        private readonly ILogger<OrganizationClient> _logger;
        private readonly HttpClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrganizationClient"/> class
        /// </summary>
        /// <param name="httpClient">HttpClient from default httpclientfactory</param>
        /// <param name="generalSettings">the general settings</param>
        /// <param name="logger">the logger</param>
        public OrganizationClient(HttpClient httpClient, IOptions<GeneralSettings> generalSettings, ILogger<OrganizationClient> logger)
        {
            _generalSettings = generalSettings.Value;
            _logger = logger;
            _client = httpClient;
        }

        /// <inheritdoc />
        public async Task<Organization> GetOrganization(string orgNr)
        {
            Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}organizations/{orgNr}");

            HttpResponseMessage response = await _client.GetAsync(endpointUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return await JsonSerializer.DeserializeAsync<Organization>(await response.Content.ReadAsStreamAsync());
            }
            else
            {
                _logger.LogError("Getting org with org nr {OrgNr} failed with statuscode {StatusCode}", orgNr, response.StatusCode);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<OrgContactPointsList> GetContactPoints(OrgContactPointLookup lookup)
        {
            Uri endpointUrl = new($"{_generalSettings.BridgeApiEndpoint}organizations/contactpoints");

            BridgeOrgContactPointLookup bridgeLookup = new()
            {
                OrganisationNumbers = lookup.OrganizationNumbers
            };

            StringContent requestBody = new(JsonSerializer.Serialize(bridgeLookup), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _client.PostAsync(endpointUrl, requestBody);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return await JsonSerializer.DeserializeAsync<OrgContactPointsList>(await response.Content.ReadAsStreamAsync());
            }
            else
            {
                _logger.LogError("Getting contact points for orgs failed with statuscode {StatusCode}", response.StatusCode);
                throw await PlatformHttpException.CreateAsync(response);
            }
        }
    }
}
