using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Operations;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

            return services;
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
            services.TryAddScoped<LookupV1PartyFromA2RequestHandler>();
            services.TryAddScoped<LookupV1PartyFromDBRequestHandler>();
            services.TryAddScoped<LookupV1PartyNamesFromA2RequestHandler>();
            services.TryAddScoped<LookupV1PartyNamesFromDBRequestHandler>();
        }
    }
}
