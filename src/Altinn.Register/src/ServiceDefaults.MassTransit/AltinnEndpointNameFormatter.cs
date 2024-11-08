using MassTransit;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// A <see cref="IEndpointNameFormatter"/> for altinn services.
/// </summary>
internal sealed class AltinnEndpointNameFormatter(AltinnServiceDescriptor descriptor)
        : KebabCaseEndpointNameFormatter(prefix: descriptor.Name, includeNamespace: false)
{
    /// <inheritdoc/>
    protected override string GetConsumerName(Type type)
    {
        return base.GetConsumerName(type);
    }

    /// <inheritdoc/>
    protected override string GetMessageName(Type type)
    {
        return base.GetMessageName(type);
    }
}
