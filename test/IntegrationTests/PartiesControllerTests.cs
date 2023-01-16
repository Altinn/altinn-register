using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Altinn.Register.Configuration;
using Altinn.Register.Controllers;
using Altinn.Register.Tests.IntegrationTests.Utils;
using Altinn.Register.Tests.Mocks;
using Altinn.Register.Tests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Altinn.Platform.Register.Models;
using Altinn.Platform.Register.Enums;
using System.Text.Json;
using Altinn.Register.Services.Interfaces;
using Moq;
using Altinn.Platform.Profile.Models;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using System.Net.Http.Json;

namespace Altinn.Register.Tests.IntegrationTests
{
    public class PartiesControllerTests : IClassFixture<WebApplicationFactory<PartiesController>>
    {
        private readonly WebApplicationFactorySetup<PartiesController> _webApplicationFactorySetup;

        public PartiesControllerTests(WebApplicationFactory<PartiesController> factory)
        {
            _webApplicationFactorySetup = new WebApplicationFactorySetup<PartiesController>(factory);

            GeneralSettings generalSettings = new() { BridgeApiEndpoint = "http://localhost/" };
            _webApplicationFactorySetup.GeneralSettingsOptions.Setup(s => s.Value).Returns(generalSettings);
        }

        [Fact]
        public async Task GetPartyList_ValidInput_OK()
        {
            // Arrange
            List<int> partyIds = new List<int>();
            partyIds.Add(50004216);
            partyIds.Add(50004219);

            HttpRequestMessage sblRequest = null;
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                sblRequest = request;
                List<Party> partyList = new List<Party>();

                foreach (int id in partyIds)
                {
                    partyList.Add(await TestDataLoader.Load<Party>(id.ToString()));
                }
                
                return new HttpResponseMessage() { Content = JsonContent.Create(partyList) };
            });
            _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

            string token = PrincipalUtil.GetToken(1);

            HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist") { Content = requestBody };
            testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(testRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Test for GetPartyListForUser with no data for fail partyids
        /// </summary>
        [Fact]
        public async Task GetPartyListForPartyIds_InvalidInput_Notfound()
        {
            // Arrange
            List<int> partyIds = new List<int>();
            partyIds.Add(1);
            partyIds.Add(2);

            HttpRequestMessage sblRequest = null;
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                sblRequest = request;
                return await Task.FromResult(new HttpResponseMessage() { StatusCode = HttpStatusCode.NotFound });
            });
            _webApplicationFactorySetup.SblBridgeHttpMessageHandler = messageHandler;

            string token = PrincipalUtil.GetToken(1);

            HttpClient client = _webApplicationFactorySetup.GetTestServerClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            StringContent requestBody = new StringContent(JsonSerializer.Serialize(partyIds), Encoding.UTF8, "application/json");

            HttpRequestMessage testRequest = new HttpRequestMessage(HttpMethod.Post, "/register/api/v1/parties/partylist") { Content = requestBody };
            testRequest.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "unittest"));

            // Act
            HttpResponseMessage response = await client.SendAsync(testRequest);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private static async Task<HttpResponseMessage> CreateHttpResponseMessage(object obj)
        {
            string content = JsonSerializer.Serialize(obj);
            StringContent stringContent = new StringContent(content, Encoding.UTF8, "application/json");
            return await Task.FromResult(new HttpResponseMessage { Content = stringContent });
        }

        private Party GetParty(int partyId, PartyType partyType)
        {
            if (partyType == PartyType.Organisation)
            {
                return new Party()
                {
                    PartyId = partyId,
                    PartyTypeName = partyType,
                    OrgNumber = "945325674",
                    Name = "OrgA"
                };
            }
            else if (partyType == PartyType.Person)
            {
                return new Party()
                {
                    PartyId = partyId,
                    PartyTypeName = partyType,
                    SSN = "12076822341",
                    Name = "PersonA"
                };
            }

            return null;
        }
    }
}
