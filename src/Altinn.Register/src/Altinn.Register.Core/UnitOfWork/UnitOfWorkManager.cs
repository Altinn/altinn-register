using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Core.UnitOfWork;

/// <summary>
/// Implementation of <see cref="IUnitOfWorkManager"/>.
/// </summary>
internal class UnitOfWorkManager
    : IUnitOfWorkManager
{
    private readonly Impl _impl;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWorkManager"/> class.
    /// </summary>
    public UnitOfWorkManager(
        Impl cache,
        IServiceProvider services)
    {
        _impl = cache;
        _services = services;
    }

    /// <inheritdoc/>
    public ValueTask<IUnitOfWork> CreateAsync(
        ReadOnlySpan<KeyValuePair<string, object?>> tags = default,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string activityName = "")
        => _impl.CreateAsync(RegisterActivitySource.StartActivity(ActivityKind.Internal, activityName, tags), _services, cancellationToken);

    /// <summary>
    /// The actual unit-of-work implementation.
    /// </summary>
    internal sealed class Impl
    {
        private readonly ImmutableArray<IUnitOfWorkParticipantFactory> _participants;
        private readonly FrozenDictionary<Type, Func<UnitOfWork, object>> _services;
        private readonly int _serviceCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Impl"/> class.
        /// </summary>
        public Impl(
            IEnumerable<IUnitOfWorkParticipantFactory> participants,
            IEnumerable<IUnitOfWorkServiceFactory> services)
        {
            _participants = participants.ToImmutableArray();

            var builder = new List<KeyValuePair<Type, Func<UnitOfWork, object>>>();
            for (var i = 0; i < _participants.Length; i++)
            {
                var participantFactory = _participants[i];
                foreach (var type in participantFactory.ServiceTypes)
                {
                    builder.Add(KeyValuePair.Create(type, UnitOfWork.CreateParticipantLookup(i, type)));
                }
            }

            var serviceIndex = 0;
            foreach (var serviceFactory in services)
            {
                var anyType = false;
                foreach (var iface in serviceFactory.GetType().GetInterfaces())
                {
                    if (IsServiceFactoryInterface(iface, out var serviceType))
                    {
                        anyType = true;
                        builder.Add(KeyValuePair.Create(serviceType, UnitOfWork.CreateServiceActivator(serviceIndex++, serviceFactory, serviceType)));
                    }
                }

                if (!anyType)
                {
                    ThrowHelper.ThrowInvalidOperationException($"Service factory {serviceFactory.GetType()} does not implement IUnitOfWorkServiceFactory<T> for any T");
                }
            }

            _services = builder.ToFrozenDictionary();
            _serviceCount = serviceIndex;

            static bool IsServiceFactoryInterface(Type iface, [NotNullWhen(true)] out Type? serviceType)
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IUnitOfWorkServiceFactory<>))
                {
                    serviceType = iface.GetGenericArguments()[0];
                    return true;
                }

                serviceType = null;
                return false;
            }
        }

        /// <summary>
        /// Creates a new unit of work.
        /// </summary>
        /// <param name="activity">An optional <see cref="Activity"/>.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/>.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>A <see cref="IUnitOfWork"/>.</returns>
        public ValueTask<IUnitOfWork> CreateAsync(
            Activity? activity,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default)
        {
            List<Task<(IUnitOfWorkParticipant Participant, int Index)>>? pending = null;
            var builder = ImmutableArray.CreateBuilder<IUnitOfWorkParticipant?>(_participants.Length);

            for (var i = 0; i < _participants.Length; i++)
            {
                var factory = _participants[i];
                var participant = factory.Create(serviceProvider, cancellationToken);
                if (participant.IsCompleted)
                {
                    builder.Add(participant.Result);
                }
                else
                {
                    builder.Add(null);
                    pending ??= new(_participants.Length);
                    pending.Add(CreatePendingTask(participant, i));
                }
            }

            if (pending is not null)
            {
                return WaitForPending(activity, pending, builder, this, serviceProvider, cancellationToken);
            }

            return new(Create(activity, builder, this, serviceProvider));

            static IUnitOfWork Create(
                Activity? activity,
                ImmutableArray<IUnitOfWorkParticipant?>.Builder participants,
                Impl impl,
                IServiceProvider serviceProvider)
            {
                Debug.Assert(participants.Count == participants.Capacity);
                Debug.Assert(participants.All(p => p is not null));

                return new UnitOfWork(activity, serviceProvider, participants.MoveToImmutable()!, impl);
            }

            static async ValueTask<IUnitOfWork> WaitForPending(
                Activity? activity,
                List<Task<(IUnitOfWorkParticipant Participant, int Index)>> pending,
                ImmutableArray<IUnitOfWorkParticipant?>.Builder builder,
                Impl impl,
                IServiceProvider serviceProvider,
                CancellationToken cancellationToken)
            {
                var completed = await Task.WhenAll(pending);
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var (participant, index) in completed)
                {
                    builder[index] = participant;
                }

                return Create(activity, builder, impl, serviceProvider);
            }

            static async Task<(IUnitOfWorkParticipant Participant, int Index)> CreatePendingTask(
                ValueTask<IUnitOfWorkParticipant> pendingParticipant,
                int index)
            {
                var participant = await pendingParticipant;
                return (participant, index);
            }
        }

        /// <summary>
        /// Gets a service from the unit of work.
        /// </summary>
        /// <param name="serviceProvider">The fallback <see cref="IServiceProvider"/>.</param>
        /// <param name="unitOfWork">The <see cref="UnitOfWork"/>.</param>
        /// <param name="serviceType">The service type.</param>
        /// <returns>A service of type <paramref name="serviceType"/>.</returns>
        private object? GetService(IServiceProvider serviceProvider, UnitOfWork unitOfWork, Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var activator))
            {
                return activator(unitOfWork);
            }

            return serviceProvider.GetService(serviceType);
        }

        /// <summary>
        /// Gets a required service from the unit of work.
        /// </summary>
        /// <param name="serviceProvider">The fallback <see cref="IServiceProvider"/>.</param>
        /// <param name="unitOfWork">The <see cref="UnitOfWork"/>.</param>
        /// <param name="serviceType">The service type.</param>
        /// <returns>A service of type <paramref name="serviceType"/>.</returns>
        private object GetRequiredService(IServiceProvider serviceProvider, UnitOfWork unitOfWork, Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var activator))
            {
                return activator(unitOfWork);
            }

            return serviceProvider.GetRequiredService(serviceType);
        }

        /// <summary>
        /// Rents a buffer for services.
        /// </summary>
        /// <returns>A rented <see langword="object"/> array.</returns>
        private object?[] RentServices()
        {
            var services = ArrayPool<object?>.Shared.Rent(_serviceCount);

            services.AsSpan().Clear();

            return services;
        }

        /// <summary>
        /// Releases a buffer for services.
        /// </summary>
        /// <param name="services">An <see langword="object"/> array previously returned from <see cref="RentServices"/>.</param>
        private async ValueTask DisposeServices(object?[] services)
        {
            for (var i = 0; i < _serviceCount; i++)
            {
                switch (services[i])
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;

                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }

            services.AsSpan().Clear();
            ArrayPool<object?>.Shared.Return(services);
        }

        private sealed class UnitOfWork
            : IUnitOfWork
        {
            internal static Func<UnitOfWork, object> CreateParticipantLookup(int index, Type serviceType)
                => (Func<UnitOfWork, object>)_createParticipantLookupGeneric.MakeGenericMethod(serviceType).Invoke(null, [index])!;

            private static MethodInfo _createParticipantLookupGeneric = typeof(UnitOfWork).GetMethod(nameof(CreateParticipantLookupGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

            private static Func<UnitOfWork, object> CreateParticipantLookupGeneric<TService>(int index)
                where TService : class
            {
                return self =>
                {
                    var participant = self._participants[index];

                    Debug.Assert(participant is IUnitOfWorkParticipant<TService>);

                    return ((IUnitOfWorkParticipant<TService>)participant).Service;
                };
            }

            internal static Func<UnitOfWork, object> CreateServiceActivator(int index, IUnitOfWorkServiceFactory factory, Type serviceType)
                => (Func<UnitOfWork, object>)_createServiceActivatorGeneric.MakeGenericMethod(serviceType).Invoke(null, [index, factory])!;

            private static MethodInfo _createServiceActivatorGeneric = typeof(UnitOfWork).GetMethod(nameof(CreateServiceActivatorGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

            private static Func<UnitOfWork, object> CreateServiceActivatorGeneric<TService>(int index, IUnitOfWorkServiceFactory<TService> factory)
                where TService : class
            {
                return self =>
                {
                    ref var serviceSlot = ref self._services[index];

                    serviceSlot ??= factory.Create(self);

                    return serviceSlot!;
                };
            }

            private readonly Activity? _activity;
            private readonly IServiceProvider _serviceProvider;
            private readonly ImmutableArray<IUnitOfWorkParticipant> _participants;
            private readonly Impl _impl;
            private object?[] _services;

            private volatile UnitOfWorkStatus _status = UnitOfWorkStatus.Pending;

            public UnitOfWork(
                Activity? activity,
                IServiceProvider serviceProvider,
                ImmutableArray<IUnitOfWorkParticipant> participants,
                Impl impl)
            {
                _activity = activity;
                _serviceProvider = serviceProvider;
                _participants = participants;
                _impl = impl;

                _services = _impl.RentServices();
            }

            public UnitOfWorkStatus Status => _status;

#if DEBUG
            ~UnitOfWork()
            {
                if (_status != UnitOfWorkStatus.Disposed)
                {
                    Debug.Fail($"Unit of work was not disposed: {_activity?.DisplayName ?? "<no display name>"}");
                }
            }
#endif

            public async ValueTask DisposeAsync()
            {
#if DEBUG
                GC.SuppressFinalize(this);
#endif

                if (_status == UnitOfWorkStatus.Disposed)
                {
                    return;
                }

                _activity?.Dispose();
                _status = UnitOfWorkStatus.Disposed;

                foreach (var participant in _participants)
                {
                    await participant.DisposeAsync();
                }

                await _impl.DisposeServices(_services);
                _services = null!;
            }

            object? IServiceProvider.GetService(Type serviceType)
                => _impl.GetService(_serviceProvider, this, serviceType);

            object ISupportRequiredService.GetRequiredService(Type serviceType)
                => _impl.GetRequiredService(_serviceProvider, this, serviceType);

            public ValueTask CommitAsync(CancellationToken cancellationToken = default)
            {
                var status = CheckDisposed();

                if (status == UnitOfWorkStatus.Committed)
                {
                    return ValueTask.CompletedTask;
                }

                if (status != UnitOfWorkStatus.Pending)
                {
                    ThrowHelper.ThrowInvalidOperationException($"Cannot commit unit of work in state {_status}");
                }

                _status = UnitOfWorkStatus.Committed;
                return Inner(_participants, cancellationToken);

                static async ValueTask Inner(ImmutableArray<IUnitOfWorkParticipant> participants, CancellationToken cancellationToken)
                {
                    foreach (var participant in participants)
                    {
                        await participant.CommitAsync(cancellationToken);
                    }
                }
            }

            public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
            {
                var status = CheckDisposed();

                if (status == UnitOfWorkStatus.RolledBack)
                {
                    return ValueTask.CompletedTask;
                }

                if (status != UnitOfWorkStatus.Pending)
                {
                    ThrowHelper.ThrowInvalidOperationException($"Cannot rollback unit of work in state {_status}");
                }

                _status = UnitOfWorkStatus.RolledBack;
                return Inner(_participants, cancellationToken);

                static async ValueTask Inner(ImmutableArray<IUnitOfWorkParticipant> participants, CancellationToken cancellationToken)
                {
                    foreach (var participant in participants)
                    {
                        await participant.RollbackAsync(cancellationToken);
                    }
                }
            }

            private UnitOfWorkStatus CheckDisposed()
            {
                var status = _status;
                if (status == UnitOfWorkStatus.Disposed)
                {
                    ThrowHelper.ThrowObjectDisposedException(nameof(UnitOfWork));
                }

                return status;
            }
        }
    }
}
