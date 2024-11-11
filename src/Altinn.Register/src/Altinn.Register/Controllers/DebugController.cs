#nullable enable

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using Altinn.Register.Configuration;
using Altinn.Register.Conventions;
using CommunityToolkit.Diagnostics;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nerdbank.Streams;

namespace Altinn.Register.Controllers;

/// <summary>
/// Proxy to SBL bridge for test environments.
/// </summary>
[ApiController]
[DevTestCondition]
[Authorize(Policy = "Debug")]
[Route("register/api/v0/debug")]
[ApiExplorerSettings(IgnoreApi = true)]
public class DebugController
    : ControllerBase
{
    private readonly IOptions<GeneralSettings> _generalSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebugController"/> class.
    /// </summary>
    public DebugController(
        IOptions<GeneralSettings> generalSettings)
    {
        _generalSettings = generalSettings;
    }

    /// <summary>
    /// test
    /// </summary>
    [HttpGet("queue-import")]
    [LocalDevCondition]
    public async Task<IActionResult> Test(
        [FromQuery(Name = "partyUuid")] Guid partyUuid,
        [FromQuery(Name = "changeId")] int changeId,
        [FromServices] IBus bus,
        [FromServices] IMessageScheduler scheduler,
        CancellationToken cancellationToken = default)
    {
        var command = new PartyImport.ImportA2PartyCommand
        {
            PartyUuid = partyUuid,
            ChangeId = changeId,
            ChangedTime = DateTimeOffset.UtcNow,
        };

        // TODO: send, not publish
        await bus.Publish(command, cancellationToken);
        return Accepted($"Queued import of {partyUuid}");
    }

    /// <summary>
    /// Gets the party changes.
    /// </summary>
    /// <param name="changeId">The change id to start from.</param>
    [HttpGet("parties/partychanges/{changeId:int}")]
    public IActionResult PartyChanges(int changeId)
        => ForwardTo($"parties/partychanges/{changeId}");

    /// <summary>
    /// Gets the roles provided by a party.
    /// </summary>
    /// <param name="partyId">The party id.</param>
    [HttpGet("parties/partyroles/{partyId:int}")]
    public IActionResult PartyRoles(int partyId)
        => ForwardTo($"parties/partyroles/{partyId}");

    /// <summary>
    /// Gets the party.
    /// </summary>
    /// <param name="partyId">The party id.</param>
    [HttpGet("parties/{partyId:int}")]
    public IActionResult Party(int partyId)
        => ForwardTo($"parties/{partyId}");

    [NonAction]
    private HttpProxyResult ForwardTo(string path)
    {
        HttpRequestMessage? request = null;

        try
        {
            var settings = _generalSettings.Value;
            var endpointUrlBuilder = new UriBuilder($"{settings.BridgeApiEndpoint}{path}");

            if (Request.QueryString.HasValue)
            {
                endpointUrlBuilder.Query = MergeQueryString(endpointUrlBuilder.Query, Request.QueryString);
            }

            var method = HttpMethod.Parse(Request.Method);
            request = new HttpRequestMessage(method, endpointUrlBuilder.Uri);

            var result = new HttpProxyResult(request);
            request = null;
            return result;
        }
        finally
        {
            request?.Dispose();
        }
    }

    [NonAction]
    private static string MergeQueryString(string query, QueryString toAppend)
    {
        Debug.Assert(toAppend.HasValue);

        if (string.IsNullOrWhiteSpace(query))
        {
            return toAppend.Value;
        }

        return string.Create(query.Length + toAppend.Value.Length, (query, toAppend), (span, args) =>
        {
            var (query, toAppend) = args;
            query.AsSpan().CopyTo(span);
            span[query.Length] = '&';
            toAppend.Value.AsSpan().CopyTo(span.Slice(query.Length + 1));
        });
    }

    private sealed class HttpProxyResult
        : IActionResult
        , IDisposable
    {
        private readonly HttpRequestMessage _request;

        public HttpProxyResult(HttpRequestMessage request)
        {
            _request = request;
        }

        public void Dispose()
        {
            _request.Dispose();
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            using var client = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DebugController));
            using var request = _request;
            using var data = new Sequence<byte>(ArrayPool<byte>.Shared);
            await CopyBody(context.HttpContext.Request.BodyReader, data, context.HttpContext.RequestAborted);
            using var content = new StreamContent(data.AsReadOnlySequence.AsStream());
            request.Content = content;

            using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);

            context.HttpContext.Response.StatusCode = (int)response.StatusCode;
            CopyHeaders(response.Headers, context.HttpContext.Response);
            CopyHeaders(response.Content.Headers, context.HttpContext.Response);
            await response.Content.CopyToAsync(context.HttpContext.Response.Body, context.HttpContext.RequestAborted);
        }

        private void CopyHeaders(HttpHeaders headers, HttpResponse response)
        {
            foreach (var (name, values) in headers)
            {
                if (IsIgnoredHeader(name))
                {
                    continue;
                }
                
                foreach (var value in values)
                {
                    response.Headers.Append(name, value);
                }
            }
        }

        private static async Task CopyBody(PipeReader reader, Sequence<byte> writer, CancellationToken cancellationToken)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                try
                {
                    if (result.IsCanceled)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ThrowHelper.ThrowOperationCanceledException(CancellationToken.None);
                    }

                    writer.Write(buffer);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                finally
                {
                    // Advance even if WriteAsync throws so the PipeReader is not left in the
                    // currently reading state
                    reader.AdvanceTo(buffer.End);
                }
            }
        }

        private static bool IsIgnoredHeader(string name)
        {
            return name switch
            {
                "PlatformAccessToken" => true,
                "Authorization" => true,
                _ => false,
            };
        }
    }
}
