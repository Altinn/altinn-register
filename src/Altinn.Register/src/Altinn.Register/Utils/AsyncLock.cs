#nullable enable

namespace Altinn.Register.Utils;

/// <summary>
/// An async-enabled lock.
/// </summary>
internal sealed class AsyncLock()
    : AsyncConcurrencyLimiter(1)
{
}
