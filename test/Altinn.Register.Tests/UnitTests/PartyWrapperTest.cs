﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Register.Enums;
using Altinn.Platform.Register.Models;
using Altinn.Register.Configuration;
using Altinn.Register.Services.Implementation;
using Altinn.Register.Tests.Mocks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Register.Tests.UnitTests
{
    public class PartyWrapperTest
    {
        private readonly Mock<IOptions<GeneralSettings>> _generalSettingsOptions = new();
        private readonly Mock<ILogger<PartiesWrapper>> _partyWrapperLogger = new();
        private readonly IMemoryCache _memoryCache;

        public PartyWrapperTest()
        {
            GeneralSettings generalSettings = new() { BridgeApiEndpoint = "http://localhost/" };
            _generalSettingsOptions.Setup(s => s.Value).Returns(generalSettings);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [Fact]
        public async Task GetParty_SblBridge_finds_party_Target_returns_Party()
        {
            // Arrange
            HttpRequestMessage sblRequest = null;
            Guid partyUuid = new Guid("4c3b4909-eb17-45d5-bde1-256e065e196a");
            string cacheKey = $"PartyUUID:{partyUuid}";
            bool inCache = _memoryCache.TryGetValue(cacheKey, out Party fromCache);
                
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                sblRequest = request;
                Party party = new Party
                {
                    PartyId = 50002114,
                    PartyUuid = new Guid("4c3b4909-eb17-45d5-bde1-256e065e196a"),
                    PartyTypeName = PartyType.Person,
                    OrgNumber = string.Empty,
                    SSN = "01025161013",
                    Name = "ELENA FJÆR",
                    IsDeleted = false,
                    OnlyHierarchyElementWithNoAccess = false,
                    Person = new Person
                    {
                        SSN = "01025161013",
                        Name = "ELENA FJÆR",
                        FirstName = "ELENA",
                        LastName = "FJÆR",
                        MailingAddress = "Søreidåsen 3 5252 SØREIDGREND",
                        MailingPostalCode = "5252",
                        MailingPostalCity = "SØREIDGREND",
                        AddressStreetName = "Søreidåsen",
                        AddressHouseNumber = "3",
                        AddressPostalCode = "5252",
                        AddressCity = "SØREIDGREND"
                    }
                };
                return await CreateHttpResponseMessage(party);
            });
            
            var target = new PartiesWrapper(new HttpClient(messageHandler), _generalSettingsOptions.Object, _partyWrapperLogger.Object, _memoryCache);

            // Act
            var actual = await target.GetPartyByUuid(partyUuid);

            // Assert
            Assert.NotNull(actual);
            Assert.False(inCache);
            Assert.Equal(partyUuid, actual.PartyUuid);
            inCache = _memoryCache.TryGetValue(cacheKey, out fromCache);
            Assert.True(inCache);
            Assert.Equal(partyUuid, fromCache.PartyUuid);

            Assert.NotNull(sblRequest);
            Assert.Equal(HttpMethod.Get, sblRequest.Method);
            Assert.EndsWith($"parties?partyuuid=4c3b4909-eb17-45d5-bde1-256e065e196a", sblRequest.RequestUri!.ToString());
        }

        [Fact]
        public async Task GetParty_SblBridge_returns_NotFound_Target_returns_Null()
        {
            // Arrange
            HttpRequestMessage sblRequest = null;
            Guid partyUuid = new Guid("4c3b4909-eb17-45d5-bde1-256e065e196a");
            string cacheKey = $"PartyUUID:{partyUuid}";
            bool inCache = _memoryCache.TryGetValue(cacheKey, out Party fromCache);

            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                sblRequest = request;
                return await CreateHttpErrorResponse(HttpStatusCode.NotFound);
            });

            var target = new PartiesWrapper(new HttpClient(messageHandler), _generalSettingsOptions.Object, _partyWrapperLogger.Object, _memoryCache);

            // Act
            var actual = await target.GetPartyByUuid(partyUuid);

            // Assert
            Assert.Null(actual);
            Assert.False(inCache);
            
            Assert.NotNull(sblRequest);
            Assert.Equal(HttpMethod.Get, sblRequest.Method);
            Assert.EndsWith($"parties?partyuuid=4c3b4909-eb17-45d5-bde1-256e065e196a", sblRequest.RequestUri!.ToString());
        }

        [Fact]
        public async Task GetParty_SblBridge_finds_partylist_Target_returns_PartyList()
        {
            // Arrange
            HttpRequestMessage sblRequest = null;
            List<Guid> partyUuids = new List<Guid> { new("4c3b4909-eb17-45d5-bde1-256e065e196a"), new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };
            
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                sblRequest = request;
                List<Party> partyList = new List<Party>
                {
                    new()
                    {
                        PartyId = 50002114,
                        PartyUuid = new Guid("4c3b4909-eb17-45d5-bde1-256e065e196a"),
                        PartyTypeName = PartyType.Person,
                        SSN = "01025161013",
                        Name = "ELENA FJÆR",
                        IsDeleted = false,
                        OnlyHierarchyElementWithNoAccess = false,
                        Person = new Person
                        {
                            SSN = "01025161013",
                            Name = "ELENA FJÆR",
                            FirstName = "ELENA",
                            LastName = "FJÆR",
                        }
                    },
                    new()
                    {
                        PartyId = 50002118,
                        PartyUuid = new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"),
                        PartyTypeName = PartyType.Person,
                        SSN = "01035922055",
                        Name = "MIE FORSMO",
                        IsDeleted = false,
                        OnlyHierarchyElementWithNoAccess = false,
                        Person = new()
                        {
                            SSN = "01035922055",
                            Name = "MIE FORSMO",
                            FirstName = "MIE",
                            LastName = "FORSMO"
                        }
                    },
                    new()
                    {
                        PartyId = 50002119,
                        PartyUuid = new("e622554e-3de5-44cd-a822-c66024768013"),
                        PartyTypeName = PartyType.Person,
                        SSN = "01035942080",
                        Name = "MARGIT ROLAND",
                        IsDeleted = false,
                        OnlyHierarchyElementWithNoAccess = false,
                        Person = new()
                        {
                            SSN = "01035942080",
                            Name = "MARGIT ROLAND",
                            FirstName = "MARGIT",
                            LastName = "ROLAND",
                        }
                    }
                };
                
                return await CreateHttpResponseMessage(partyList);
            });

            var target = new PartiesWrapper(new HttpClient(messageHandler), _generalSettingsOptions.Object, _partyWrapperLogger.Object, _memoryCache);

            // Act
            var actual = await target.GetPartyListByUuid(partyUuids);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(partyUuids[0], actual[0].PartyUuid);
            Assert.Equal(partyUuids[1], actual[1].PartyUuid);
            Assert.Equal(partyUuids[2], actual[2].PartyUuid);

            Assert.NotNull(sblRequest);
            Assert.Equal(HttpMethod.Post, sblRequest.Method);
            Assert.EndsWith($"parties/byuuid?fetchsubunits=false", sblRequest.RequestUri!.ToString().ToLower());
        }

        [Fact]
        public async Task GetParty_SblBridge_returns_NotFound_Target_returns_EmptyList()
        {
            // Arrange
            HttpRequestMessage sblRequest = null;
            List<Guid> partyUuids = new List<Guid> { new("4c3b4909-eb17-45d5-bde1-256e065e196a"), new("93630d41-ca61-4b5c-b8fb-3346b561f6ff"), new("e622554e-3de5-44cd-a822-c66024768013") };

            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                sblRequest = request;
                
                return await CreateHttpErrorResponse(HttpStatusCode.NotFound);
            });

            var target = new PartiesWrapper(new HttpClient(messageHandler), _generalSettingsOptions.Object, _partyWrapperLogger.Object, _memoryCache);

            // Act
            var actual = await target.GetPartyListByUuid(partyUuids);

            // Assert
            Assert.Empty(actual);
            
            Assert.NotNull(sblRequest);
            Assert.Equal(HttpMethod.Post, sblRequest.Method);
            Assert.EndsWith($"parties/byuuid?fetchsubunits=false", sblRequest.RequestUri!.ToString().ToLower());
        }

        private static async Task<HttpResponseMessage> CreateHttpResponseMessage(object obj)
        {
            string content = JsonSerializer.Serialize(obj);
            StringContent stringContent = new StringContent(content, Encoding.UTF8, "application/json");
            return await Task.FromResult(new HttpResponseMessage { Content = stringContent });
        }

        private static async Task<HttpResponseMessage> CreateHttpErrorResponse(HttpStatusCode responseCode)
        {
            return await Task.FromResult(new HttpResponseMessage { StatusCode = responseCode });
        }
    }
}
