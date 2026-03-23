#nullable enable

using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Contracts.V1;
using Altinn.Register.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Altinn.Register.Tests.UnitTests;

public class PersonLookupServiceTests
{
    private readonly Mock<IPersonClient> _persons;
    private readonly Mock<ILogger<PersonLookupService>> _logger;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private readonly TestClock _clock;
    private readonly MemoryCache _memoryCache;
    private readonly PersonLookupSettings _lookupSettings;

    public PersonLookupServiceTests()
    {
        _persons = new Mock<IPersonClient>();
        _lookupSettings = new PersonLookupSettings();

        _clock = new TestClock();
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = _clock,
        });
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
        _persons.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);

        var target = new PersonLookupService(_persons.Object, Options.Create(_lookupSettings), _memoryCache, _logger.Object);

        // Act
        var actual = await target.GetPerson("personnumber", "lastname", 777, CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPerson_OneFailedAttempt_MoreToGo_CorrectInput_ReturnsParty()
    {
        // Arrange
        Person person = new Person
        {
            LastName = "lastname"
        };
        _persons.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
        _lookupSettings.MaximumFailedAttempts = 2;

        var target = new PersonLookupService(_persons.Object, Options.Create(_lookupSettings), _memoryCache, _logger.Object);

        // Act
        await Should.NotThrowAsync(() => target.GetPerson("personnumber", "wrongname", 777, CancellationToken));
        var actual = await target.GetPerson("personnumber", "lastname", 777, CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPerson_OneFailedAttempt_MoreToGo_WrongInput_ReturnsNull()
    {
        // Arrange
        _persons.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Person?)null);
        _lookupSettings.MaximumFailedAttempts = 2;

        var target = new PersonLookupService(_persons.Object, Options.Create(_lookupSettings), _memoryCache, _logger.Object);

        // Act
        await Should.NotThrowAsync(() => target.GetPerson("personnumber", "wrongname", 777, CancellationToken));
        var actual = await target.GetPerson("personnumber", "lastname", 777, CancellationToken);

        // Assert
        actual.ShouldBeNull();
    }

    [Fact]
    public async Task GetPerson_TooManyFailedAttempts_CorrectInput_ThrowsTooManyFailedLookupsException()
    {
        // Arrange
        Person person = new Person
        {
            LastName = "lastname"
        };
        _persons.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
        _lookupSettings.MaximumFailedAttempts = 1;

        var target = new PersonLookupService(_persons.Object, Options.Create(_lookupSettings), _memoryCache, _logger.Object);

        // Act
        await Should.NotThrowAsync(() => target.GetPerson("personnumber", "wrongname", 777, CancellationToken));
        await Should.ThrowAsync<TooManyFailedLookupsException>(() => target.GetPerson("personnumber", "lastname", 777, CancellationToken));

        // Assert
    }

    [Fact]
    public async Task GetPerson_WrongInput_FailedAttemptsBeingResetAtCacheTimeout()
    {
        // Arrange
        Person person = new Person
        {
            LastName = "lastname"
        };
        _persons.Setup(s => s.GetPerson(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
        _lookupSettings.MaximumFailedAttempts = 2;
        _lookupSettings.FailedAttemptsCacheLifetimeSeconds = 100;

        var target = new PersonLookupService(_persons.Object, Options.Create(_lookupSettings), _memoryCache, _logger.Object);

        // Act
        await Should.NotThrowAsync(() => target.GetPerson("personnumber", "wrongname", 777, CancellationToken));
        await Should.NotThrowAsync(() => target.GetPerson("personnumber", "wrongname", 777, CancellationToken));
        await Should.ThrowAsync<TooManyFailedLookupsException>(() => target.GetPerson("personnumber", "wrongname", 777, CancellationToken));

        // Assert
        _clock.Advance(TimeSpan.FromSeconds(_lookupSettings.FailedAttemptsCacheLifetimeSeconds + 1));
        await Should.NotThrowAsync(() => target.GetPerson("personnumber", "wrongname", 777, CancellationToken));
    }

    private class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

        public void Advance(TimeSpan duration)
        {
            UtcNow += duration;
        }
    }
}
