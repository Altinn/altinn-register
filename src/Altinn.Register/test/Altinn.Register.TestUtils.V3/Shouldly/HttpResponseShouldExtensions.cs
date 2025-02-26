using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Altinn.Register.TestUtils.Http;
using Altinn.Register.TestUtils.Utils;
using Nerdbank.Streams;

namespace Shouldly;

[DebuggerStepThrough]
[ShouldlyMethods]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class HttpResponseShouldExtensions
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task ShouldHaveSuccessStatusCode(this HttpResponseMessage response, string? customMessage = null)
    {
        if (!response.IsSuccessStatusCode)
        {
            var responseData = await ResponseData.Read(response, TestContext.Current.CancellationToken);
            throw new ShouldAssertException(new HttpResponseActualShouldlyMessage(responseData, response.StatusCode, customMessage).ToString());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task ShouldHaveStatusCode(this HttpResponseMessage response, HttpStatusCode expected, string? customMessage = null)
    {
        if (response.StatusCode != expected)
        {
            var responseData = await ResponseData.Read(response, TestContext.Current.CancellationToken);
            throw new ShouldAssertException(new HttpResponseActualShouldlyMessage(responseData, response.StatusCode, expected, customMessage).ToString());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<T> ShouldHaveJsonContent<T>(this HttpResponseMessage response, string? customMessage = null)
    {
        var content = await BufferContent(response, TestContext.Current.CancellationToken);

        try
        {
            var parsed = await content.ReadFromJsonAsync<T>();
            if (parsed is null)
            {
                var responseData = await ResponseData.Read(response, TestContext.Current.CancellationToken);
                throw new ShouldAssertException(new HttpResponseActualShouldlyMessage(responseData, "null", customMessage).ToString());
            }

            return parsed;
        }
        catch (JsonException ex)
        {
            var responseData = await ResponseData.Read(response, TestContext.Current.CancellationToken);
            throw new ShouldAssertException(new HttpResponseActualShouldlyMessage(responseData, ex, customMessage).ToString());
        }
    }

    private static async Task<SequenceHttpContent> BufferContent(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is SequenceHttpContent preBuffered)
        {
            return preBuffered;
        }

        Sequence<byte>? buffer = null;
        SequenceHttpContent? sequenceContent = null;

        try
        {
            buffer = new(ArrayPool<byte>.Shared);
            await response.Content.CopyToAsync(buffer.AsStream(), cancellationToken);
            sequenceContent = new SequenceHttpContent(buffer);
            buffer = null;

            foreach (var h in response.Content.Headers)
            {
                sequenceContent.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            var result = sequenceContent;
            response.Content = result;
            sequenceContent = null;
            return result;
        }
        finally
        {
            sequenceContent?.Dispose();
            buffer?.Dispose();
        }
    }

    private sealed class ResponseData
    {
        public Version Version { get; }

        public HttpStatusCode StatusCode { get; }

        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; }

        public string Content { get; }

        public ResponseData(Version version, HttpStatusCode statusCode, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string content)
        {
            Version = version;
            StatusCode = statusCode;
            Headers = headers;
            Content = content;
        }

        public static async Task<ResponseData> Read(HttpResponseMessage message, CancellationToken cancellationToken)
        {
            var buffered = await BufferContent(message, cancellationToken);

            return new(
                message.Version,
                message.StatusCode,
                message.Headers.Concat(buffered.Headers),
                await buffered.ReadAsStringAsync(cancellationToken));
        }
    }

    private sealed class HttpResponseActualShouldlyMessage
        : ShouldlyMessage
    {
        private readonly ResponseData _response;

        public HttpResponseActualShouldlyMessage(ResponseData response, object? actual, string? customMessage = null, [CallerMemberName] string shouldlyMethod = null!)
        {
            _response = response;
            ShouldlyAssertionContext = new ShouldlyAssertionContext(shouldlyMethod)
            {
                Actual = actual,
            };

            if (customMessage != null)
            {
                ShouldlyAssertionContext.CustomMessage = customMessage;
            }
        }

        public HttpResponseActualShouldlyMessage(ResponseData response, object? actual, object? expected, string? customMessage = null, [CallerMemberName] string shouldlyMethod = null!)
            : this(response, actual, customMessage, shouldlyMethod)
        {
            ShouldlyAssertionContext.Expected = expected;
        }

        public override string ToString()
        {
            var context = ShouldlyAssertionContext;
            var codePart = context.CodePart;
            var actual = context.Actual;
            var expected = context.Expected;
            
            var actualString = 
                $"""

                {actual}
                """;

            var expectedString = expected is null
                ? string.Empty
                : $"""

                   {StringHelpers.ToStringAwesomely(expected)}
                """;

            var message =
                $"""
                 {codePart}
                     {StringHelpers.PascalToSpaced(context.ShouldMethod)}{expectedString}
                     but was{actualString}
                 """;

            if (ShouldlyAssertionContext.CustomMessage != null)
            {
                message += $"""


                        Additional Info:
                            {ShouldlyAssertionContext.CustomMessage}
                        """;
            }

            message +=
                $"""

                ===== RESPONSE =====
                {PrintResponse(_response)}
                """;

            return message;
        }

        private static string PrintResponse(ResponseData message)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HTTP/{message.Version} {message.StatusCode}");
            foreach (var header in message.Headers)
            {
                sb.Append($"{header.Key}: ").AppendJoin(", ", header.Value).AppendLine();
            }

            sb.AppendLine().AppendLine(message.Content);
            return sb.ToString();
        }
    }
}
