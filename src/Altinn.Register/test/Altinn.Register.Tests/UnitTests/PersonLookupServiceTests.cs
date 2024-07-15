#nullable enable

using System.Threading;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Core;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Register.Tests.UnitTests;

public class PersonLookupServiceTests
{
    private readonly Mock<IPersonClient> _persons;
    private readonly Mock<IOptions<PersonLookupSettings>> _settingsMock;
    private readonly Mock<ILogger<PersonLookupService>> _logger;

    private readonly MemoryCache memoryCache;
    private readonly PersonLookupSettings lookupSettings;

    public PersonLookupServiceTests()
    {
        _persons = new Mock<IPersonClient>();
        lookupSettings = new PersonLookupSettings();

        _settingsMock = new Mock<IOptions<PersonLookupSettings>>();
        _settingsMock.Setup(s => s.Value).Returns(lookupSettings);

        memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = new Mock<ILogger<PersonLookupService>>();
    }

    [Fact]
    public async Task GetPerson_NoFailedAttempts_CorrectInput_ReturnsParty()
    {
        // Arrange
        Person person = new Person
        {
            LastName = "lastname"
        };
        _persons.Setup(s => s.GetPerson(It.IsAny<string>())).ReturnsAsync(person);

        var target = new PersonLookupService(_persons.Object, _settingsMock.Object, memoryCache, _logger.Object);

        // Act
        var actual = await target.GetPerson("personnumber", "lastname", 777);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(0, memoryCache.Get<int>("Person-Lookup-Failed-Attempts777"));
    }

    [Fact]
    public async Task GetPerson_OneFailedAttempt_MoreToGo_CorrectInput_ReturnsParty()
    {
        // Arrange
        Person person = new Person
        {
            LastName = "lastname"
        };
        _persons.Setup(s => s.GetPerson(It.IsAny<string>())).ReturnsAsync(person);
        memoryCache.Set("Person-Lookup-Failed-Attempts777", 1);
        lookupSettings.MaximumFailedAttempts = 2;

        var target = new PersonLookupService(_persons.Object, _settingsMock.Object, memoryCache, _logger.Object);

        // Act
        var actual = await target.GetPerson("personnumber", "lastname", 777);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(1, memoryCache.Get<int>("Person-Lookup-Failed-Attempts777"));
    }

    [Fact]
    public async Task GetPerson_OneFailedAttempt_MoreToGo_WrongInput_ReturnsNull()
    {
        // Arrange
        _persons.Setup(s => s.GetPerson(It.IsAny<string>())).ReturnsAsync((Person?)null);
        memoryCache.Set("Person-Lookup-Failed-Attempts777", 1);
        lookupSettings.MaximumFailedAttempts = 2;

        var target = new PersonLookupService(_persons.Object, _settingsMock.Object, memoryCache, _logger.Object);

        // Act
        var actual = await target.GetPerson("personnumber", "lastname", 777);

        // Assert
        Assert.Null(actual);
        Assert.Equal(2, memoryCache.Get<int>("Person-Lookup-Failed-Attempts777"));
    }

    [Fact]
    public async Task GetPerson_TooManyFailedAttempts_CorrectInput_ThrowsTooManyFailedLookupsException()
    {
        // Arrange
        Person person = new Person
        {
            LastName = "lastname"
        };
        _persons.Setup(s => s.GetPerson(It.IsAny<string>())).ReturnsAsync(person);
        memoryCache.Set("Person-Lookup-Failed-Attempts777", 1);
        lookupSettings.MaximumFailedAttempts = 1;

        var target = new PersonLookupService(_persons.Object, _settingsMock.Object, memoryCache, _logger.Object);

        TooManyFailedLookupsException? actual = null;

        // Act
        try
        {
            _ = await target.GetPerson("personnumber", "lastname", 777);
        }
        catch (TooManyFailedLookupsException tomfle)
        {
            actual = tomfle;
        }

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(1, memoryCache.Get<int>("Person-Lookup-Failed-Attempts777"));
    }

    [Fact]
    public async Task GetPerson_WrongInput_FailedAttemptsBeingResetAtCacheTimeout()
    {
        // Arrange
        Person person = new Person
        {
            LastName = "lastname"
        };
        _persons.Setup(s => s.GetPerson(It.IsAny<string>())).ReturnsAsync(person);
        memoryCache.Set("Person-Lookup-Failed-Attempts777", 1);
        lookupSettings.MaximumFailedAttempts = 2;
        lookupSettings.FailedAttemptsCacheLifetimeSeconds = 1;

        var target = new PersonLookupService(_persons.Object, _settingsMock.Object, memoryCache, _logger.Object);

        // Act
        _ = await target.GetPerson("personnumber", "wrongname", 777);

        try
        {
            _ = await target.GetPerson("personnumber", "wrongname", 777);
        }
        catch
        {
        }

        // Assert
        Thread.Sleep(1200);
        Assert.Equal(0, memoryCache.Get<int>("Person-Lookup-Failed-Attempts777"));
    }
}
