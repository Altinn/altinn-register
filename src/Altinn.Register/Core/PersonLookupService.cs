#nullable enable
using System;
using System.Threading.Tasks;

using Altinn.Platform.Register.Models;
using Altinn.Register.Clients.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Core
{
    /// <summary>
    /// Represents the business logic related to checking if a national identity number is in use.
    /// </summary>
    public class PersonLookupService : IPersonLookup
    {
        private const string PersonLookupFailedAttempts = "Person-Lookup-Failed-Attempts";

        private readonly IPersonClient _personsService;
        private readonly PersonLookupSettings _personLookupSettings;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<PersonLookupService> _logger;

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
        }

        /// <summary>
        /// Operation for checking if a given national identity number is connected to a person.
        /// </summary>
        /// <param name="nationalIdentityNumber">The national identity number to check.</param>
        /// <param name="lastName">The last name of the person. Must match the last name of the person.</param>
        /// <param name="activeUser">The unique id of the user performing the check.</param>
        /// <returns>The identified <see cref="Task{Party}"/> if last name was correct.</returns>
        public async Task<Person?> GetPerson(string nationalIdentityNumber, string lastName, int activeUser)
        {
            string uniqueCacheKey = PersonLookupFailedAttempts + activeUser;

            _ = _memoryCache.TryGetValue(uniqueCacheKey, out int failedAttempts);
            if (failedAttempts >= _personLookupSettings.MaximumFailedAttempts)
            {
                _logger.LogInformation(
                    "User {userId} has performed too many failed person lookup attempts.", activeUser);

                throw new TooManyFailedLookupsException();
            }

            Person? person = await _personsService.GetPerson(nationalIdentityNumber);

            string nameFromParty = person?.LastName ?? string.Empty;

            if (nameFromParty.Length > 0 && nameFromParty.IsSimilarTo(lastName))
            {
                return person;
            }

            failedAttempts += 1;
            MemoryCacheEntryOptions memoryCacheOptions = new()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_personLookupSettings.FailedAttemptsCacheLifetimeSeconds)
            };
            _memoryCache.Set(uniqueCacheKey, failedAttempts, memoryCacheOptions);
            return null;
        }
    }
}
