using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.Events;

namespace PolarSharp.Webhooks.Extensions;

/// <summary>
/// Provides the <c>[ValidatePolarWebhook]</c> action filter for MVC controllers.
/// </summary>
/// <remarks>
/// <para>
/// For Minimal API applications, webhook verification is handled automatically by
/// <c>UsePolarInfrastructure()</c>. This filter is for MVC controller applications.
/// </para>
/// <para>
/// <strong>Dispatch semantics:</strong> The filter verifies the HMAC signature, runs all
/// configured security pre-checks (HTTPS, Content-Type, payload size, IP allowlist), then
/// dispatches the verified event through <see cref="IPolarWebhookDispatcher"/> using a
/// <strong>non-cancellable</strong> <see cref="CancellationTokenSource"/>. This prevents
/// HTTP client disconnect from aborting in-flight payment fulfillment mid-transaction.
/// The action method body is <strong>bypassed</strong> after dispatch — its sole purpose
/// is to provide a route and OpenAPI metadata. Do <em>not</em> put fulfillment logic in
/// the action; register an <see cref="IPolarWebhookHandler{TEvent}"/> through
/// <c>AddWebhookHandler&lt;TEvent, THandler&gt;()</c> instead.
/// </para>
/// <para>
/// The verified <see cref="WebhookEvent"/> is stored in
/// <c>HttpContext.Items["PolarWebhookEvent"]</c> for any downstream inspection.
/// </para>
/// <example>
/// <code>
/// [ApiController]
/// [Route("hooks")]
/// public class WebhookController : ControllerBase
/// {
///     // Action body is bypassed after filter dispatch — route and metadata only.
///     [HttpPost("polar")]
///     [ValidatePolarWebhook]
///     public IActionResult ReceiveWebhook() => Ok();
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class ValidatePolarWebhookAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Runs all webhook security pre-checks, verifies the Polar HMAC-SHA256 signature,
    /// dispatches the verified event, and returns HTTP 200. The decorated action method
    /// body is never executed.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    /// <param name="next">The next action execution delegate (not invoked on success).</param>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var services = context.HttpContext.RequestServices;
        var opts     = services.GetRequiredService<IOptionsMonitor<PolarWebhookOptions>>().CurrentValue;
        var request  = context.HttpContext.Request;

        // ── Security pre-checks (mirrors Minimal API path) ──────────────────────

        // HTTPS enforcement — 400, NOT redirect (Polar sender does not follow redirects).
        if (opts.RequireHttps && !context.HttpContext.Request.IsHttps)
        {
            context.Result = new ObjectResult("HTTPS is required.") { StatusCode = 400 };
            return;
        }

        // Content-Type enforcement.
        var ct = request.ContentType;
        if (string.IsNullOrEmpty(ct) ||
            !ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new StatusCodeResult(415);
            return;
        }

        // Payload size limit — fast path via Content-Length header.
        var contentLength = request.ContentLength;
        if (contentLength.HasValue && contentLength.Value > opts.MaxPayloadBytes)
        {
            context.Result = new StatusCodeResult(413);
            return;
        }

        // ── Read body ────────────────────────────────────────────────────────────

        request.EnableBuffering();

        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms).ConfigureAwait(false);
        request.Body.Position = 0;
        var bodyBytes = ms.ToArray();

        // Actual size check (when Content-Length was absent).
        if (bodyBytes.Length > opts.MaxPayloadBytes)
        {
            context.Result = new StatusCodeResult(413);
            return;
        }

        // ── HMAC signature verification ──────────────────────────────────────────

        var webhookId        = request.Headers["webhook-id"].ToString();
        var webhookTimestamp = request.Headers["webhook-timestamp"].ToString();
        var webhookSignature = request.Headers["webhook-signature"].ToString();

        var validator          = services.GetRequiredService<WebhookValidator>();
        var verificationResult = validator.Verify(webhookId, webhookTimestamp, webhookSignature, bodyBytes);

        if (verificationResult.IsFailure)
        {
            context.Result = new ObjectResult(new { error = "Webhook verification failed." })
            {
                StatusCode = 400,
            };
            return;
        }

        var evt = verificationResult.Match(onSuccess: static e => e, onFailure: static _ => null!);
        context.HttpContext.Items["PolarWebhookEvent"] = evt;

        // ── Dispatch with non-cancellable scope (payment-safety) ────────────────
        // A new CancellationTokenSource (not HttpContext.RequestAborted) is used so that
        // HTTP client disconnect cannot abort mid-transaction payment fulfillment.
        var dispatcher = services.GetRequiredService<IPolarWebhookDispatcher>();
        using var cts  = new CancellationTokenSource();
        await dispatcher.DispatchAsync(evt, cts.Token).ConfigureAwait(false);

        // Bypass the action body — dispatch is complete.
        context.Result = new OkResult();
    }
}
