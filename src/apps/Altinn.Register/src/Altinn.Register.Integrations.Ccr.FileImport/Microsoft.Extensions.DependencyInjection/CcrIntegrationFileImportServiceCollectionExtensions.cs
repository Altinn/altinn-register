using Altinn.Register.Core.Ccr;
using Altinn.Register.Integrations.Ccr.FileImport;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class CcrIntegrationFileImportServiceCollectionExtensions
{
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers services for CCR file processing.
        /// </summary>
        /// <returns><paramref name="services"/>.</returns>
        public IServiceCollection AddCcrFileProcessor()
        {
            services.TryAddSingleton<ICcrFlatFileProcessor, CcrFlatFileProcessorFactory>();

            return services;
        }
    }
}
