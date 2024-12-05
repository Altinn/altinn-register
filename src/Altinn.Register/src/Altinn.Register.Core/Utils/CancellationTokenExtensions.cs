#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Core.Utils;

/// <summary>
/// Extension methods for <see cref="CancellationToken"/>.
/// </summary>
public static class CancellationTokenExtensions
{
    /// <summary>
    /// Returns a <see cref="CancellationTokenWaiter"/> that can be awaited to asynchronously wait for the <see cref="CancellationToken"/> to be canceled.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="CancellationTokenWaiter"/> that can be awaited.</returns>
    public static CancellationTokenWaiter WaitForCancellationAsync(this CancellationToken cancellationToken)
    {
        return new CancellationTokenWaiter(cancellationToken);
    }

    /// <summary>
    /// Awaits the <see cref="CancellationToken"/> to be canceled.
    /// </summary>
    public readonly struct CancellationTokenWaiter
    {
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancellationTokenWaiter"/> struct.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        internal CancellationTokenWaiter(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets an awaiter for this <see cref="CancellationTokenWaiter"/>.
        /// </summary>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CancellationTokenAwaiter GetAwaiter()
        {
            return new CancellationTokenAwaiter(_cancellationToken);
        }
    }

    /// <summary>
    /// Async/await awaiter for <see cref="CancellationToken"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct CancellationTokenAwaiter
        : INotifyCompletion
        , ICriticalNotifyCompletion
    {
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancellationTokenAwaiter"/> struct.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        public CancellationTokenAwaiter(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the result of the asynchronous wait operation.
        /// </summary>
        public void GetResult() 
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                ThrowHelper.ThrowInvalidOperationException("The awaiter was not awaited.");
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="CancellationToken"/> has been canceled.
        /// </summary>
        public bool IsCompleted => _cancellationToken.IsCancellationRequested;

        /// <inheritdoc/>
        public void OnCompleted(Action action)
            => _cancellationToken.Register(action);

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action action)
            => _cancellationToken.Register(action, useSynchronizationContext: false);
    }
}
