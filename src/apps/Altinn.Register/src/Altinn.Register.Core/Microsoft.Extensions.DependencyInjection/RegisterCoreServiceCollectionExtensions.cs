#pragma warning disable CS1734 // The compiler associates XML docs on the extension block with the lowered static method, even though the documented 'services' receiver parameter is valid.

using System.Collections.Concurrent;
using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Operations;
using Altinn.Register.Core.RateLimiting;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register services from the Altinn.Register.Core assembly.
/// </summary>
public static class RegisterCoreServiceCollectionExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Add core services from register.
        /// </summary>
        /// <returns><paramref name="services"/>.</returns>
        public IServiceCollection AddRegisterCoreServices()
        {
            services.AddApiSourceSwitchProvider();
            services.AddMediator();
            services.AddRegisterOperations();
            services.AddRegisterRateLimiting();

            return services;
        }

        /// <summary>
        /// Add core rate-limiting services from register.
        /// </summary>
        /// <returns><paramref name="services"/>.</returns>
        public IServiceCollection AddRegisterRateLimiting()
        {
            services.AddRateLimiter();

            return services;
        }

        /// <summary>
        /// Adds and binds a named rate-limit policy from configuration.
        /// </summary>
        /// <param name="name">The policy name.</param>
        /// <returns>The <see cref="OptionsBuilder{TOptions}"/>.</returns>
        public OptionsBuilder<RateLimitPolicySettings> AddRateLimitPolicy(string name)
        {
            Guard.IsNotNullOrWhiteSpace(name);

            services.AddRegisterRateLimiting();

            var builder = services.AddOptions<RateLimitPolicySettings>(name);

            if (RateLimitPolicyMarker.TryAdd(name, services))
            {
                builder.BindConfiguration($"Altinn:Register:RateLimit:Policy:{name}")
                    .Configure(static settings => settings.IsConfigured = true);
            }

            return builder;
        }

        private void AddMediator()
        {
            services.TryAddSingleton<RegisterMediator>();

            var ifaces = typeof(RegisterMediator.Sender).GetInterfaces()
                .Where(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IRequestSender<,>));

            foreach (var iface in ifaces)
            {
                services.TryAddTransient(iface, typeof(RegisterMediator.Sender));
            }
        }

        private void AddApiSourceSwitchProvider()
        {
            services.TryAddSingleton<ApiSourceSwitchProvider>();
        }

        private void AddRegisterOperations()
        {
            services.TryAddScoped<GetOrganizationFromA2RequestHandler>();
            services.TryAddScoped<GetOrganizationFromDBRequestHandler>();
            services.TryAddScoped<GetV1PartyByIdFromA2RequestHandler>();
            services.TryAddScoped<GetV1PartyByIdFromDBRequestHandler>();
            services.TryAddScoped<GetV1PartyByUuidFromA2RequestHandler>();
            services.TryAddScoped<GetV1PartyByUuidFromDBRequestHandler>();
            services.TryAddScoped<GetV1PersonFromA2RequestHandler>();
            services.TryAddScoped<GetV1PersonFromDBRequestHandler>();
            services.TryAddScoped<LookupV1PartyFromA2RequestHandler>();
            services.TryAddScoped<LookupV1PartyFromDBRequestHandler>();
            services.TryAddScoped<LookupV1PartyNamesFromA2RequestHandler>();
            services.TryAddScoped<LookupV1PartyNamesFromDBRequestHandler>();
        }

        private void AddRateLimiter()
        {
            services.AddOptions();
            services.TryAddSingleton<IRateLimiter, RateLimiter>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RateLimitPolicySettings>, RateLimitPolicySettingsValidator>());
        }
    }

    private sealed class RateLimitPolicyMarker
    {
        private static readonly ConcurrentDictionary<string, ServiceDescriptor> _markers = new();

        public static bool TryAdd(string name, IServiceCollection services)
        {
            var descriptor = _markers.GetOrAdd(name, static name => new ServiceDescriptor(typeof(RateLimitPolicyMarker), name, new RateLimitPolicyMarker()));
            if (services.Contains(descriptor))
            {
                return false;
            }

            services.Add(descriptor);
            return true;
        }
    }
}
