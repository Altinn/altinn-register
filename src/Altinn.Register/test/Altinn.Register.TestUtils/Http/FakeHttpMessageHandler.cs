using System.Collections.Concurrent;
using System.Text;
using CommunityToolkit.Diagnostics;
using Xunit.Sdk;

namespace Altinn.Register.TestUtils.Http;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> that can be used for testing.
/// </summary>
public class FakeHttpMessageHandler
    : HttpMessageHandler
{
    /// <summary>
    /// Gets the root path used for routing by the fake message handler. Any requests made to
    /// the fake message handler will have to be relative to this path in order to match any
    /// routes.
    /// </summary>
    public static readonly Uri FakeBasePath = new Uri("https://fake.example.com/fake/root/");

    private readonly Lock _lock = new();

    private readonly Queue<IFakeRequestHandler> _expectations = new();
    private readonly List<IFakeRequestHandler> _handlers = new();
    private readonly FallbackFakeRequestHandler _fallback = new();

    private readonly List<FakeRequestContext> _requests = new();
    private readonly ConcurrentQueue<TaskCompletionSource> _pending = new();

    private bool _autoFlush = false;

    /// <summary>
    /// Gets the fallback handler.
    /// </summary>
    public ISetFakeRequestHandler Fallback => _fallback;

    /// <summary>
    /// Adds a request handler to the expectations queue.
    /// </summary>
    /// <param name="handler">The expected request handler.</param>
    public void Expect(IFakeRequestHandler handler)
    {
        _expectations.Enqueue(handler);
    }

    /// <summary>
    /// Asserts that all expectations have been met.
    /// </summary>
    public void AssertAllExpectationsMet()
    {
        lock (_lock)
        {
            if (_expectations.Count > 0)
            {
                var expectations = _expectations.ToArray();
                throw new ExpectationNotMetException(expectations);
            }
        }
    }

    /// <summary>
    /// Creates a <see cref="HttpClient"/> with the fake handler.
    /// </summary>
    /// <returns>A <see cref="HttpClient"/>.</returns>
    public HttpClient CreateClient()
    {
        var client = new HttpClient(this);
        client.BaseAddress = FakeBasePath;
        client.DefaultRequestHeaders.Add("X-Fake-Base-Path", FakeBasePath.ToString());

        return client;
    }

    /// <inheritdoc/>
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return ThrowHelper.ThrowNotSupportedException<HttpResponseMessage>("Only synchronous Send operations are supported.");
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = await FakeRequestContext.Create(this, request, cancellationToken);
        var handler = RecordRequestAndFindHandler(context);

        var hold = _autoFlush;
        if (hold)
        {
            var pending = new TaskCompletionSource();
            _pending.Enqueue(pending);
            await pending.Task;
        }

        await handler.Handle(context, cancellationToken);
        return context.Response;
    }

    private IFakeRequestHandler RecordRequestAndFindHandler(FakeRequestContext context)
    {
        lock (_lock)
        {
            _requests.Add(context);
            
            if (_expectations.TryPeek(out var expectation))
            {
                if (!expectation.CanHandle(context))
                {
                    throw new UnexpectedRequestException(context, expectation);
                }

                _expectations.Dequeue();
                return expectation;
            }

            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(context))
                {
                    return handler;
                }
            }

            // fallback can handle all requests
            return _fallback;
        }
    }

    /// <summary>
    /// Holds all requests until <see cref="Flush"/> is called.
    /// </summary>
    public void HoldRequests()
    {
        _autoFlush = true;
    }

    /// <summary>
    /// Releases all requests that are being held.
    /// </summary>
    public void Flush()
    {
        while (_pending.TryDequeue(out var pending))
        {
            pending.SetResult();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AssertAllExpectationsMet();
        }

        base.Dispose(disposing);
    }

    private sealed class FallbackFakeRequestHandler
        : BaseFakeRequestHandler
    {
        private static readonly FakeRequestDelegate DefaultHandler
            = static (context, cancellationToken) =>
            {
                // TODO: Throw match summary exception.
                throw new NotImplementedException();
            };

        public FallbackFakeRequestHandler()
            : base(DefaultHandler)
        {
        }

        protected override bool CanHandle(FakeRequestContext context)
            => true;

        protected override string Description
            => $"Fallback handler for requests that do not match any other handler.";
    }

    private sealed class ExpectationNotMetException
        : Exception
        , IAssertionException
    {
        public ExpectationNotMetException(IReadOnlyList<IFakeRequestHandler> expectations)
            : base(CreateMessage(expectations))
        {
            Expectations = expectations;
        }

        public IReadOnlyList<IFakeRequestHandler> Expectations { get; }

        private static string CreateMessage(IReadOnlyList<IFakeRequestHandler> expectations)
        {
            var sb = new StringBuilder("Not all http requests were made as expected. The following expectations were not met:");

            foreach (var expectation in expectations)
            {
                sb.AppendLine();
                sb.Append(" - ");
                sb.Append(expectation.Description);
            }

            return sb.ToString();
        }
    }
}
