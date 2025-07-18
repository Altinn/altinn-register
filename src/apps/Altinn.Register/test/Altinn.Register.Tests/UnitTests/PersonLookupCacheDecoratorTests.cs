﻿#nullable enable
using Altinn.Register.Contracts.V1;
using Altinn.Register.Core;
using Altinn.Register.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Register.Tests.UnitTests
{
    public class PersonLookupCacheDecoratorTests
    {
        private readonly Mock<IPersonLookup> _personLookup;
        private readonly Mock<IOptions<PersonLookupSettings>> _personLookupSettingsOptions;

        private readonly MemoryCache _memoryCache;
        private readonly PersonLookupSettings _personLookupSettings;

        public PersonLookupCacheDecoratorTests()
        {
            _personLookup = new Mock<IPersonLookup>();
            _personLookupSettings = new PersonLookupSettings();

            _personLookupSettingsOptions = new Mock<IOptions<PersonLookupSettings>>();
            _personLookupSettingsOptions.Setup(s => s.Value).Returns(_personLookupSettings);

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [Fact]
        public async Task GetPerson_NoCache_UseDecoratedService_ReturnsPerson()
        {
            // Arrange
            Person person = new()
            {
                LastName = "lastname"
            };
            _personLookup.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(person);

            var target = new PersonLookupCacheDecorator(
                _personLookup.Object, _memoryCache, _personLookupSettingsOptions.Object);

            // Act
            var actual = await target.GetPerson("personnumber", "lastname", 777);

            // Assert
            _personLookup.Verify(
                s => s.GetPerson(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.NotNull(actual);
            Assert.NotNull(_memoryCache.Get<Person?>("GetPerson_personnumber_lastname"));
        }

        [Fact]
        public async Task GetPerson_HaveCache_DoesNotUseDecoratedService_ReturnsPerson()
        {
            // Arrange
            Person person = new()
            {
                LastName = "lastname"
            };
            _personLookup.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(person);

            _memoryCache.Set("GetPerson_personnumber_lastname", person);

            var target = new PersonLookupCacheDecorator(
                _personLookup.Object, _memoryCache, _personLookupSettingsOptions.Object);

            // Act
            var actual = await target.GetPerson("personnumber", "lastname", 777);

            // Assert
            _personLookup.Verify(
                s => s.GetPerson(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

            Assert.NotNull(actual);
            Assert.NotNull(_memoryCache.Get<Person?>("GetPerson_personnumber_lastname"));
        }

        [Fact]
        public async Task GetPerson_NoCache_DecoratedServiceReturnsNull_ReturnsNull()
        {
            // Arrange
            _personLookup.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Person?)null);

            var target = new PersonLookupCacheDecorator(
                _personLookup.Object, _memoryCache, _personLookupSettingsOptions.Object);

            // Act
            var actual = await target.GetPerson("personnumber", "lastname", 777);

            // Assert
            _personLookup.Verify(
                s => s.GetPerson(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.Null(actual);
            Assert.Null(_memoryCache.Get<Person?>("GetPerson_personnumber_lastname"));
        }
    }
}
