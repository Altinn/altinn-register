using CommunityToolkit.Diagnostics;

namespace Altinn.Register.TestUtils;

internal static class AsyncLazyReferenceCounted
{
    public static AsyncLazyReferenceCounted<T> Create<T>(bool allowReuse = true)
        where T : notnull, IAsyncResource<T>
        => new(static () => T.New(), static value => value.DisposeAsync(), allowReuse);
}

internal sealed class AsyncLazyReferenceCounted<T>
    : IDisposable
    where T : notnull
{
    private readonly AsyncLock _lock = new();
    private readonly Func<ValueTask<T>> _factory;
    private readonly Func<T, ValueTask> _destructor;
    private readonly bool _allowReuse;

    private int _referenceCount = -1;
    private T? _value;

    public AsyncLazyReferenceCounted(
        Func<ValueTask<T>> factory,
        Func<T, ValueTask> destructor,
        bool allowReuse = true)
    {
        Guard.IsNotNull(factory);
        Guard.IsNotNull(destructor);

        _factory = factory;
        _destructor = destructor;
        _allowReuse = allowReuse;
    }

    public async Task<IAsyncRef<T>> Get()
        => await Acquire();

    public void Dispose()
    {
        _lock.Dispose();
    }

    private async Task<Reference> Acquire()
    {
        using var guard = await _lock.Acquire();
        if (_referenceCount < -1 && !_allowReuse)
        {
            ThrowHelper.ThrowInvalidOperationException($"AsyncLazyReferenceCounted<{typeof(T).Name}> attempted reuse");
        }

        if (_referenceCount < 0)
        {
            _value = await _factory();
            _referenceCount = 0;
        }

        _referenceCount++;
        return new Reference(this);
    }

    private async Task Release()
    {
        using var guard = await _lock.Acquire();
        if (--_referenceCount == 0)
        {
            await _destructor(_value!);
            _referenceCount = -2;
        }
    }

    private class Reference
        : IAsyncRef<T>
    {
        private int _disposed = 0;
        private AsyncLazyReferenceCounted<T> _rc;

        public T Value 
        {
            get
            {
                if (Volatile.Read(ref _disposed) == 1)
                {
                    ThrowHelper.ThrowObjectDisposedException($"IAsyncRef<{typeof(T).Name}>");
                }

                return _rc._value!;
            }
        }

        internal Reference(AsyncLazyReferenceCounted<T> rc)
        {
            Guard.IsNotNull(rc);

            _rc = rc;
        }

        ~Reference()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                ThrowHelper.ThrowInvalidOperationException($"IAsyncRef<{typeof(T).Name}> was not disposed");
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                var result = _rc.Release();
                _rc = default!;
                GC.SuppressFinalize(this);
                return new(result);
            }

            return ValueTask.CompletedTask;
        }

        bool IAsyncRef.IsDisposed => Volatile.Read(ref _disposed) == 1;
    }
}
