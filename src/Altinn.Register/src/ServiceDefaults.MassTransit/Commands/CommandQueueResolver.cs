using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using MassTransit;

namespace Altinn.Authorization.ServiceDefaults.MassTransit.Commands;

/// <summary>
/// Implementation of <see cref="ICommandQueueResolver"/>.
/// </summary>
internal class CommandQueueResolver
    : ICommandQueueResolver
    , ICommandQueueRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, CommandQueueInfo> _builder = new();

    private int _builderVersion = 0;
    private int _frozenVersion = -1;
    private FrozenDictionary<RuntimeTypeHandle, Uri>? _frozen;

    /// <inheritdoc/>
    public bool TryGetQueueUriForCommandType(Type commandType, [NotNullWhen(true)] out Uri? queueUri)
        => GetFrozen().TryGetValue(commandType.TypeHandle, out queueUri);

    /// <inheritdoc/>
    void ICommandQueueRegistry.RegisterCommandQueue(Type commandType, CommandQueueInfo registration)
    {
        if (_builder.TryGetValue(commandType, out var existing))
        {
            if (existing == registration)
            {
                return;
            }

            ThrowHelper.ThrowInvalidOperationException($"Command type '{TypeCache.GetShortName(commandType)}' is handled by multiple consumers. Both {existing} and {registration} handles the command type.");
        }

        _builder.Add(commandType, registration);
        Interlocked.Increment(ref _builderVersion);
    }

    private FrozenDictionary<RuntimeTypeHandle, Uri> GetFrozen()
    {
        var builderVersion = Volatile.Read(ref _builderVersion);
        var frozenVersion = Volatile.Read(ref _frozenVersion);
        if (builderVersion == frozenVersion)
        {
            return _frozen!;
        }

        lock (_lock)
        {
            builderVersion = Volatile.Read(ref _builderVersion);
            frozenVersion = Volatile.Read(ref _frozenVersion);

            if (builderVersion == frozenVersion)
            {
                return _frozen!;
            }

            _frozen = _builder.ToFrozenDictionary(kv => kv.Key.TypeHandle, kv => kv.Value.QueueUri);
            Volatile.Write(ref _frozenVersion, builderVersion);
        }

        return _frozen!;
    }
}
