using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.TestUtils.Http.Handlers;

internal static class FakeRequestDelegateFactory
{
    internal static FakeRequestDelegate Create(Delegate handler)
    {
        if (handler is FakeRequestDelegate originalDelegate)
        {
            return originalDelegate;
        }

        var invoker = DelegateInvoker.ForReturnType(handler.Method.ReturnType);
        var parameterPopulators = handler.Method.GetParameters().Select(ParameterPopulator.ForParameter).ToImmutableArray();
        var target = handler.Target;
        var method = handler.Method;

        return Create(target, method, invoker, parameterPopulators);
    }

    private static FakeRequestDelegate Create(object? target, MethodInfo method, DelegateInvoker invoker, ImmutableArray<ParameterPopulator> parameterPopulators)
    {
        return async (context, cancellationToken) =>
        {
            var parameters = new object?[parameterPopulators.Length];

            for (var i = 0; i < parameterPopulators.Length; i++)
            {
                parameters[i] = await parameterPopulators[i].Populate(context, cancellationToken);
            }

            await invoker.Invoke(target, method, parameters, context, cancellationToken);
        };
    }

    private sealed class ParameterPopulator
    {
        public static ParameterPopulator ForParameter(ParameterInfo parameterInfo)
        {
            var type = parameterInfo.ParameterType;

            if (type == typeof(FakeRequestContext))
            {
                return new((context, _) => ValueTask.FromResult<object?>(context));
            }

            if (type == typeof(CancellationToken))
            {
                return new((_, cancellationToken) => ValueTask.FromResult<object?>(cancellationToken));
            }

            if (type.IsAssignableFrom(typeof(FakeHttpRequestMessage)))
            {
                return new((context, _) => ValueTask.FromResult<object?>(context.Request));
            }

            return ThrowHelper.ThrowNotSupportedException<ParameterPopulator>($"Unsupported parameter type: {type}");
        }

        private readonly Func<FakeRequestContext, CancellationToken, ValueTask<object?>> _populator;

        private ParameterPopulator(Func<FakeRequestContext, CancellationToken, ValueTask<object?>> populator)
        {
            _populator = populator;
        }

        public ValueTask<object?> Populate(FakeRequestContext context, CancellationToken cancellationToken)
            => _populator(context, cancellationToken);
    }

    private sealed class DelegateInvoker
    {
        public static DelegateInvoker ForReturnType(Type type)
        {
            var awaiter = Awaiter.ForReturnType(type);
            var executor = ResultExecutor.ForResultType(awaiter.ReturnType);

            return new(async (target, method, args, context, cancellationToken) =>
            {
                var rawResult = method.Invoke(target, args);
                var result = await awaiter.Await(rawResult, cancellationToken);
                await executor.ExecuteResult(result, context, cancellationToken);
            });
        }

        private readonly Func<object?, MethodInfo, object?[], FakeRequestContext, CancellationToken, ValueTask> _invoker;

        private DelegateInvoker(Func<object?, MethodInfo, object?[], FakeRequestContext, CancellationToken, ValueTask> invoker)
        {
            _invoker = invoker;
        }

        public ValueTask Invoke(object? target, MethodInfo method, object?[] parameters, FakeRequestContext context, CancellationToken cancellationToken)
            => _invoker(target, method, parameters, context, cancellationToken);
    }

    private abstract class Awaiter
    {
        public static Awaiter ForReturnType(Type type)
        {
            if (type.IsConstructedGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                Type argument;

                if (typeDefinition == typeof(ValueTask<>))
                {
                    argument = type.GetGenericArguments()[0];
                    return ForValueTaskReturnType(argument);
                }

                if (typeDefinition == typeof(Task<>))
                {
                    argument = type.GetGenericArguments()[0];
                    return ForTaskReturnType(argument);
                }
            }

            if (type == typeof(ValueTask))
            {
                return ForValueTaskReturnType(typeof(void));
            }

            if (type == typeof(Task))
            {
                return ForTaskReturnType(typeof(void));
            }

            return ForBareReturnType(type);

            static Awaiter ForBareReturnType(Type type)
            {
                var awaiterType = typeof(BareAwaiter<>).MakeGenericType(type);

                return (Awaiter)Activator.CreateInstance(awaiterType)!;
            }

            static Awaiter ForTaskReturnType(Type type)
            {
                var awaiterType = typeof(TaskAwaiter<>).MakeGenericType(type);

                return (Awaiter)Activator.CreateInstance(awaiterType)!;
            }

            static Awaiter ForValueTaskReturnType(Type type)
            {
                var awaiterType = typeof(ValueTaskAwaiter<>).MakeGenericType(type);

                return (Awaiter)Activator.CreateInstance(awaiterType)!;
            }
        }

        public abstract Type ReturnType { get; }

        public abstract ValueTask<object?> Await(object? value, CancellationToken cancellationToken);

        private abstract class TypedAwaiter<T>
            : Awaiter
        {
            public abstract ValueTask<object?> Await(T value, CancellationToken cancellationToken);

            public sealed override ValueTask<object?> Await(object? value, CancellationToken cancellationToken)
                => Await((T)value!, cancellationToken);
        }

        private sealed class BareAwaiter<T>
            : TypedAwaiter<T>
        {
            public override Type ReturnType => typeof(T);

            public override ValueTask<object?> Await(T value, CancellationToken cancellationToken)
                => ValueTask.FromResult<object?>(value);
        }

        private sealed class TaskAwaiter<T>
            : TypedAwaiter<Task<T>>
        {
            public override Type ReturnType => typeof(T);

            public override async ValueTask<object?> Await(Task<T> value, CancellationToken cancellationToken)
                => await value.WaitAsync(cancellationToken);
        }

        private sealed class ValueTaskAwaiter<T>
            : TypedAwaiter<ValueTask<T>>
        {
            public override Type ReturnType => typeof(T);

            [SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "Task is completed")]
            public override ValueTask<object?> Await(ValueTask<T> value, CancellationToken cancellationToken)
            {
                if (value.IsCompletedSuccessfully)
                {
                    return ValueTask.FromResult<object?>(value.Result);
                }

                if (cancellationToken.CanBeCanceled)
                {
                    return new(value.AsTask().WaitAsync(cancellationToken));
                }

                return WaitFor(value);
            }

            private static async ValueTask<object?> WaitFor(ValueTask<T> value)
                => await value;
        }
    }

    private abstract class ResultExecutor
    {
        public static ResultExecutor ForResultType(Type type)
        {
            if (type == typeof(void))
            {
                return new VoidResultExecutor();
            }

            if (type == typeof(string))
            {
                var inner = ForResultType(typeof(StringContent));

                return new ConvertResultExecutor<string>(static s => new StringContent(s), inner);
            }

            if (type.IsAssignableTo(typeof(HttpContent)))
            {
                return new HttpContentResultExecutor();
            }

            return ThrowHelper.ThrowNotSupportedException<ResultExecutor>($"Unsupported return type: {type}");
        }

        public abstract ValueTask ExecuteResult(object? result, FakeRequestContext context, CancellationToken cancellationToken);

        private abstract class TypedResultExecutor<T>
            : ResultExecutor
        {
            public abstract ValueTask Execute(T result, FakeRequestContext context, CancellationToken cancellationToken);

            public sealed override ValueTask ExecuteResult(object? result, FakeRequestContext context, CancellationToken cancellationToken)
                => Execute((T)result!, context, cancellationToken);
        }

        private sealed class VoidResultExecutor
            : TypedResultExecutor<object>
        {
            public override ValueTask Execute(object result, FakeRequestContext context, CancellationToken cancellationToken)
            {
                return ValueTask.CompletedTask;
            }
        }

        private sealed class ConvertResultExecutor<T>(Func<T, object?> converter, ResultExecutor inner)
            : TypedResultExecutor<T>
        {
            public override ValueTask Execute(T result, FakeRequestContext context, CancellationToken cancellationToken)
            {
                return inner.ExecuteResult(converter(result), context, cancellationToken);
            }
        }

        private sealed class HttpContentResultExecutor
            : TypedResultExecutor<HttpContent>
        {
            public override ValueTask Execute(HttpContent result, FakeRequestContext context, CancellationToken cancellationToken)
            {
                return new(context.Response.SetContent(result, cancellationToken));
            }
        }
    }
}
