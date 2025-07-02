namespace Altinn.Register.TestUtils;

internal sealed class AsyncLock()
    : AsyncConcurrencyLimiter(1)
{ 
}
