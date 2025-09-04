#nullable enable
using Altinn.Register.Contracts.V1;
using Altinn.Register.Core;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Services
{
    /// <summary>
    /// Represents the business logic related to checking if a national identity number is in use.
    /// </summary>
    public class PersonLookupCacheDecorator : IPersonLookup
    {
        private readonly IPersonLookup _decoratedService;
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _memoryCacheOptions;

        /// <summary>
        /// Initialize a new instance of the <see cref="PersonLookupService"/> class.
        /// </summary>
        public PersonLookupCacheDecorator(
            IPersonLookup decoratedService,
            IMemoryCache memoryCache,
            IOptions<PersonLookupSettings> personLookupSettings)
        {
            _decoratedService = decoratedService;
            _memoryCache = memoryCache;

            _memoryCacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(personLookupSettings.Value.PersonCacheLifetimeSeconds)
            };
        }

        /// <inheritdoc/>
        public async Task<Person?> GetPerson(
            string nationalIdentityNumber,
            string lastName,
            int activeUser,
            CancellationToken cancellationToken = default)
        {
            string uniqueCacheKey = $"GetPerson_{nationalIdentityNumber}_{lastName}";

            if (_memoryCache.TryGetValue(uniqueCacheKey, out Person? person))
            {
                return person;
            }

            person = await _decoratedService.GetPerson(nationalIdentityNumber, lastName, activeUser, cancellationToken);

            if (person is not null)
            {
                _memoryCache.Set(uniqueCacheKey, person, _memoryCacheOptions);
            }

            return person;
        }
    }
}
