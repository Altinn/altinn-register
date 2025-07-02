#nullable enable
using Altinn.Platform.Models.Register.V1;
using Altinn.Register.Clients.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Core
{
    /// <summary>
    /// Represents the business logic related to checking if a national identity number is in use.
    /// </summary>
    public partial class PersonLookupService : IPersonLookup
    {
        private const string PersonLookupFailedAttempts = "Person-Lookup-Failed-Attempts";

        private readonly IPersonClient _personsService;
        private readonly PersonLookupSettings _personLookupSettings;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<PersonLookupService> _logger;
        private readonly MemoryCacheEntryOptions _failedAttemptsCacheOptions;

        /// <summary>
        /// Initialize a new instance of the <see cref="PersonLookupService"/> class.
        /// </summary>
        public PersonLookupService(
            IPersonClient personsService,
            IOptions<PersonLookupSettings> personLookupSettings,
            IMemoryCache memoryCache,
            ILogger<PersonLookupService> logger)
        {
            _personsService = personsService;
            _personLookupSettings = personLookupSettings.Value;
            _memoryCache = memoryCache;
            _logger = logger;

            _failedAttemptsCacheOptions = new()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_personLookupSettings.FailedAttemptsCacheLifetimeSeconds),
            };
        }

        /// <inheritdoc/>
        public async Task<Person?> GetPerson(string nationalIdentityNumber, string lastName, int activeUser, CancellationToken cancellationToken = default)
        {
            ThrowIfTooManyFailedAttemptsByUser(activeUser);

            Person? person = await _personsService.GetPerson(nationalIdentityNumber, cancellationToken);

            string nameFromParty = person?.LastName ?? string.Empty;

            if (nameFromParty.Length > 0 && nameFromParty.IsSimilarTo(lastName))
            {
                return person;
            }

            IncrementFailedAttemptsByUser(activeUser);
            return null;
        }

        private void ThrowIfTooManyFailedAttemptsByUser(int activeUser)
        {
            string uniqueCacheKey = PersonLookupFailedAttempts + activeUser;

            if (_memoryCache.TryGetValue(uniqueCacheKey, out int failedAttempts) 
                && failedAttempts >= _personLookupSettings.MaximumFailedAttempts)
            {
                Log.UserHasPerformedTooManyFailedPersonLookupAttempts(_logger, activeUser);

                throw new TooManyFailedLookupsException();
            }
        }

        private void IncrementFailedAttemptsByUser(int activeUser)
        {
            string uniqueCacheKey = PersonLookupFailedAttempts + activeUser;

            if (!_memoryCache.TryGetValue(uniqueCacheKey, out int failedAttempts))
            {
                failedAttempts = 0;
            }

            _memoryCache.Set(uniqueCacheKey, failedAttempts + 1, _failedAttemptsCacheOptions);
        }

        private static partial class Log
        {
            [LoggerMessage(0, LogLevel.Information, "User {UserId} has performed too many failed person lookup attempts.")]
            public static partial void UserHasPerformedTooManyFailedPersonLookupAttempts(ILogger logger, int userId);
        }
    }
}
