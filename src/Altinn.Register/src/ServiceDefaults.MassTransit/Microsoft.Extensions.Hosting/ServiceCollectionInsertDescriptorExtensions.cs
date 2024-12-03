using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding and removing services to an <see cref="IServiceCollection" />.
/// </summary>
internal static class ServiceCollectionInsertDescriptorExtensions
{
    /// <summary>
    /// Adds a <see cref="ServiceDescriptor"/> if an existing descriptor with the same
    /// <see cref="ServiceDescriptor.ServiceType"/> and an implementation that does not already exist
    /// in <paramref name="services."/>. Does not move an existing descriptor.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="index">The index to insert the <see cref="ServiceDescriptor"/>.</param>
    /// <param name="descriptor">The <see cref="ServiceDescriptor"/>.</param>
    /// <remarks>
    /// Use <see cref="TryInsertEnumerable(IServiceCollection, int, ServiceDescriptor)"/> when registering a service implementation of a
    /// service type that
    /// supports multiple registrations of the same service type. Using
    /// <see cref="ServiceCollectionDescriptorExtensions.Add(IServiceCollection, ServiceDescriptor)"/> is not idempotent and can add
    /// duplicate
    /// <see cref="ServiceDescriptor"/> instances if called twice. Using
    /// <see cref="TryInsertEnumerable(IServiceCollection, int, ServiceDescriptor)"/> will prevent registration
    /// of multiple implementation types.
    /// </remarks>
    public static void TryInsertEnumerable(
        this IServiceCollection services,
        int index,
        ServiceDescriptor descriptor)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(descriptor);

        Type? implementationType = descriptor.GetImplementationType();

        if (implementationType == typeof(object) ||
            implementationType == descriptor.ServiceType)
        {
            ThrowHelper.ThrowArgumentException(nameof(descriptor), $"Implementation type {implementationType} for service {descriptor.ServiceType} cannot be used with TryInsertEnumerable.");
        }

        int count = services.Count;
        for (int i = 0; i < count; i++)
        {
            ServiceDescriptor service = services[i];
            if (service.ServiceType == descriptor.ServiceType &&
                service.GetImplementationType() == implementationType &&
                object.Equals(service.ServiceKey, descriptor.ServiceKey))
            {
                // Already added
                return;
            }
        }

        services.Insert(index, descriptor);
    }

    // copied directly from https://github.com/dotnet/runtime/blob/e3d9bfd6d1f5647509a843cf8dbd9d39d68263e4/src/libraries/Microsoft.Extensions.DependencyInjection.Abstractions/src/ServiceDescriptor.cs#L286-L328
    private static Type GetImplementationType(this ServiceDescriptor descriptor)
    {
        if (descriptor.ServiceKey == null)
        {
            if (descriptor.ImplementationType != null)
            {
                return descriptor.ImplementationType;
            }
            else if (descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance.GetType();
            }
            else if (descriptor.ImplementationFactory != null)
            {
                Type[]? typeArguments = descriptor.ImplementationFactory.GetType().GenericTypeArguments;

                Debug.Assert(typeArguments.Length == 2);

                return typeArguments[1];
            }
        }
        else
        {
            if (descriptor.KeyedImplementationType != null)
            {
                return descriptor.KeyedImplementationType;
            }
            else if (descriptor.KeyedImplementationInstance != null)
            {
                return descriptor.KeyedImplementationInstance.GetType();
            }
            else if (descriptor.KeyedImplementationFactory != null)
            {
                Type[]? typeArguments = descriptor.KeyedImplementationFactory.GetType().GenericTypeArguments;

                Debug.Assert(typeArguments.Length == 3);

                return typeArguments[2];
            }
        }

        Debug.Fail("ImplementationType, ImplementationInstance, ImplementationFactory or KeyedImplementationFactory must be non null");
        return null;
    }
}
