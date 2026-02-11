#nullable enable

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Register.Configuration;
using Altinn.Register.Conventions;
using Altinn.Register.PartyImport.A2;
using Asp.Versioning;
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
[ApiVersion(0.0)]
[DevTestCondition]
[Authorize(Policy = "Debug")]
[Route("register/api/v{version:apiVersion}/debug")]
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

    /// <summary>
    /// Gets trace info.
    /// </summary>
    [HttpGet("trace")]
    [AllowAnonymous]
    public ActionResult<ActivityInfo> Trace()
    {
        var activity = GetRootmost(Activity.Current);
        return new ActivityInfo(activity);

        static Activity? GetRootmost(Activity? activity)
        {
            if (activity is null)
            {
                return null;
            }

            while (activity.Parent is not null)
            {
                activity = activity.Parent;
            }

            return activity;
        }
    }

    /// <summary>
    /// Manually trigger import of a party from A2.
    /// </summary>
    /// <param name="command">The import command.</param>
    /// <param name="sender">The command sender.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    [HttpPost("import/a2")]
    public async Task<IActionResult> ImportA2Party(
        [FromBody] ImportA2PartyCommand command,
        [FromServices] ICommandSender sender,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(command, cancellationToken);

        return Ok(command);
    }

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

    /// <summary>
    /// Activity info for debugging purposes.
    /// </summary>
    /// <param name="activity">The current context.</param>
    public sealed class ActivityInfo(Activity? activity)
    {
        /// <summary>Gets the trace id.</summary>
        public string TraceId => (activity?.Context ?? default).TraceId.ToString();

        /// <summary>Gets the span id.</summary>
        public string SpanId => (activity?.Context ?? default).SpanId.ToString();

        /// <summary>Gets whether the trace is remote.</summary>
        public bool IsRemote => (activity?.Context ?? default).IsRemote;

        /// <summary>Gets the trace flags.</summary>
        public int TraceFlags => (int)(activity?.Context ?? default).TraceFlags;

        /// <summary>Gets whether the activity has a remote parent.</summary>
        public bool HasRemoteParent => activity?.HasRemoteParent ?? false;
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
