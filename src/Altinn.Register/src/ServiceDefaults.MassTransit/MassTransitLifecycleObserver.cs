using CommunityToolkit.Diagnostics;
using MassTransit;
using System.Diagnostics;

namespace Altinn.Authorization.ServiceDefaults.MassTransit;

internal sealed class MassTransitLifecycleObserver
    : IBusObserver
    , IBusLifetime
{
    private readonly TaskCompletionSource<BusReady> _tcs = new();

    /// <inheritdoc/>
    void IBusObserver.CreateFaulted(Exception exception)
    {
        _tcs.TrySetException(exception);
    }

    /// <inheritdoc/>
    void IBusObserver.PostCreate(IBus bus)
    {
    }

    /// <inheritdoc/>
    Task IBusObserver.PostStart(IBus bus, Task<BusReady> busReady)
    {
        busReady.ContinueWith(_tcs);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IBusObserver.PostStop(IBus bus)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IBusObserver.PreStart(IBus bus)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IBusObserver.PreStop(IBus bus)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IBusObserver.StartFaulted(IBus bus, Exception exception)
    {
        _tcs.TrySetException(exception);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IBusObserver.StopFaulted(IBus bus, Exception exception)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task<BusReady> IBusLifetime.WaitForBus(CancellationToken cancellationToken)
        => _tcs.Task.WaitAsync(cancellationToken);
}

internal static class TaskExtensions
{
    public static Task<T> ContinueWith<T>(this Task<T> task, TaskCompletionSource<T> source)
    {
        if (task.IsCompleted)
        {
            source.TrySetFromTask(task);
            return task;
        }
        else
        {
            task.ContinueWith(
                static (task, sourceObj) => 
                {
                    ((TaskCompletionSource<T>)sourceObj!).TrySetFromTask(task);
                    return task.Result;
                },
                source,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

#if !NET9_0_OR_GREATER
    /// <summary>
    /// Attempts to transition the underlying <see cref="Task{TResult}"/> into the same completion state as the specified <paramref name="completedTask"/>.
    /// </summary>
    /// <param name="completedTask">The completed task whose completion status (including exception or cancellation information) should be copied to the underlying task.</param>
    /// <returns><see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="completedTask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="completedTask"/> is not completed.</exception>
    /// <remarks>
    /// This operation will return false if the <see cref="Task{TResult}"/> is already in one of the three final states:
    /// <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/>, or <see cref="TaskStatus.Canceled"/>.
    /// </remarks>
    private static bool TrySetFromTask<T>(this TaskCompletionSource<T> source, Task<T> completedTask)
    {
        Guard.IsNotNull(source);
        Guard.IsNotNull(completedTask);
        if (!completedTask.IsCompleted)
        {
            ThrowHelper.ThrowArgumentException(nameof(completedTask), "The task must be completed.");
        }

        // Try to transition to the appropriate final state based on the state of completedTask.
        bool result = false;
        bool threw = false;
        switch (completedTask.Status)
        {
            case TaskStatus.RanToCompletion:
                result = source.TrySetResult(completedTask.Result);
                break;

            case TaskStatus.Canceled:
                try
                {
                    // Get the task to throw an operation canceled exception.
                    _ = completedTask.Result;
                }
                catch (OperationCanceledException ex)
                {
                    result = source.TrySetCanceled(ex.CancellationToken);
                    threw = true;
                }

                Debug.Assert(threw);
                break;

            case TaskStatus.Faulted:
                try
                {
                    // Get the task to throw an exception.
                    _ = completedTask.Result;
                }
                catch (Exception ex)
                {
                    result = source.TrySetException(ex);
                    threw = true;
                }

                Debug.Assert(threw);
                break;
        }

        return result;
    }
#endif
}
