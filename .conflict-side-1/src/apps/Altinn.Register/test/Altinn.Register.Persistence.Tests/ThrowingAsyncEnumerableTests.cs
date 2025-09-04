using Altinn.Register.Persistence.AsyncEnumerables;

namespace Altinn.Register.Persistence.Tests;

public class ThrowingAsyncEnumerableTests
{
    [Fact]
    public async Task ThrowsExceptionWithOriginalDetails()
    {
        var expected = CreateException();
        var expectedStackTrace = expected.StackTrace;

        var throwing = new ThrowingAsyncEnumerable<string>(expected);
        try
        {
            var list = await throwing.ToListAsync();
            Assert.Fail("Expected exception was not thrown");
        }
        catch (ExpectedException e)
        {
            e.StackTrace.Should().StartWith(expectedStackTrace);
        }
    }

    [Fact]
    public async Task DisposesOwnedResources()
    {
        var expected = CreateException();
        var resource1 = new TestDisposable();
        var resource2 = new TestDisposable();

        var throwing = new ThrowingAsyncEnumerable<string>(expected);
        throwing.Adopt([resource1, resource2]);

        try
        {
            var list = await throwing.ToListAsync();
            Assert.Fail("Expected exception was not thrown");
        }
        catch (ExpectedException)
        {
        }

        resource1.IsDisposed.Should().BeTrue();
        resource2.IsDisposed.Should().BeTrue();
    }

    private static ExpectedException CreateException()
    {
        try
        {
            // exceptions must be thrown to initialize the stack trace
            throw new ExpectedException("I am exception");
        }
        catch (ExpectedException e)
        {
            return e;
        }
    }

    private sealed class ExpectedException(string message)
        : Exception(message)
    {
    }

    private sealed class TestDisposable
        : IAsyncDisposable
    {
        private readonly Lock _lock = new();
        private bool _disposed;

        public bool IsDisposed
        {
            get
            {
                lock (_lock)
                {
                    return _disposed;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                _disposed = true;
            }

            return ValueTask.CompletedTask;
        }
    }
}
