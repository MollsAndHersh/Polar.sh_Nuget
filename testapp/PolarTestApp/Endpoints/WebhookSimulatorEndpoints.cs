using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks;

namespace PolarTestApp.Endpoints;

internal static class WebhookSimulatorEndpoints
{
    internal static WebApplication MapWebhookSimulatorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/test/webhook").WithTags("WebhookSimulator");

        // POST /test/webhook/simulate/{eventType}
        // Constructs a real HMAC-signed payload and POSTs it to the app's own webhook endpoint,
        // proving the full signature verification pipeline works end-to-end.
        group.MapPost("/simulate/{eventType}", async (
            string eventType,
            IOptions<PolarWebhookOptions> webhookOptions,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var opts = webhookOptions.Value;
            var secret = opts.Secret ?? opts.Secrets.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(secret))
            {
                return Results.BadRequest(new
                {
                    error = "No webhook secret configured. Set PolarSharp:Webhooks:Secret in appsettings.json."
                });
            }

            // Build a minimal valid payload for the requested event type
            var webhookId = $"wh_{Guid.NewGuid():N}";
            var webhookTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var payload = BuildSamplePayload(eventType, webhookId);
            var payloadJson = JsonSerializer.Serialize(payload);

            // Compute HMAC-SHA256 per Standard Webhooks spec:
            // sign("{webhook-id}.{webhook-timestamp}.{body}")
            var signingContent = $"{webhookId}.{webhookTimestamp}.{payloadJson}";
            var secretBytes = Convert.FromBase64String(
                secret.StartsWith("whsec_", StringComparison.OrdinalIgnoreCase)
                    ? secret["whsec_".Length..]
                    : secret);
            var sigBytes = HMACSHA256.HashData(secretBytes, Encoding.UTF8.GetBytes(signingContent));
            var signature = $"v1,{Convert.ToBase64String(sigBytes)}";

            // POST to our own webhook endpoint
            var webhookPath = opts.Path;
            var scheme = httpContext.Request.Scheme;
            var host = httpContext.Request.Host.Value;
            var targetUrl = $"{scheme}://{host}{webhookPath}";

            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl);
            request.Headers.Add("webhook-id", webhookId);
            request.Headers.Add("webhook-timestamp", webhookTimestamp);
            request.Headers.Add("webhook-signature", signature);
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, ct);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    title: "Failed to deliver simulated webhook to local endpoint.",
                    statusCode: 500);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);

            return Results.Ok(new
            {
                simulatedEventType = eventType,
                webhookId,
                webhookTimestamp,
                signature,
                targetUrl,
                responseStatusCode = (int)response.StatusCode,
                responseBody,
                success = response.IsSuccessStatusCode,
            });
        })
        .WithName("SimulateWebhook")
        .WithSummary("Simulate a Polar webhook event by constructing an HMAC-signed payload and posting it to the local webhook endpoint.")
        .WithDescription("""
            Constructs a valid HMAC-SHA256-signed webhook payload for the given event type and
            delivers it to the app's own webhook endpoint (configured via PolarSharp:Webhooks:Path).
            This proves the end-to-end signature verification pipeline works correctly.

            Requires PolarSharp:Webhooks:Secret to be configured.

            Example event types: order.created, subscription.active, subscription.canceled,
            checkout.created, customer.created, benefit.grant.created, refund.created
            """);

        return app;
    }

    private static object BuildSamplePayload(string eventType, string webhookId) => new
    {
        type = eventType,
        webhook_id = webhookId,
        created_at = DateTimeOffset.UtcNow.ToString("O"),
        data = BuildSampleData(eventType),
    };

    private static object BuildSampleData(string eventType) => eventType switch
    {
        "order.created" or "order.updated" or "order.paid" or "order.fulfilled" => new
        {
            id = $"ord_{Guid.NewGuid():N}",
            status = "confirmed",
            amount = 2999,
            currency = "usd",
            customer = new { id = $"cus_{Guid.NewGuid():N}", email = "test@example.com" },
            items = new[] { new { product_name = "Sample Product", quantity = 1, unit_price = 2999 } },
            created_at = DateTimeOffset.UtcNow.ToString("O"),
        },
        "subscription.created" or "subscription.active" or "subscription.canceled" or "subscription.revoked" => new
        {
            id = $"sub_{Guid.NewGuid():N}",
            status = "active",
            customer = new { id = $"cus_{Guid.NewGuid():N}", email = "test@example.com" },
            product = new { id = $"prod_{Guid.NewGuid():N}", name = "Sample Plan" },
            price = new { id = $"price_{Guid.NewGuid():N}", name = "Monthly" },
            created_at = DateTimeOffset.UtcNow.ToString("O"),
        },
        "customer.created" or "customer.updated" => new
        {
            id = $"cus_{Guid.NewGuid():N}",
            email = "test@example.com",
            name = "Test Customer",
            created_at = DateTimeOffset.UtcNow.ToString("O"),
        },
        "checkout.created" or "checkout.updated" => new
        {
            id = $"chk_{Guid.NewGuid():N}",
            status = "open",
            amount = 2999,
            currency = "usd",
            customer_email = "test@example.com",
            created_at = DateTimeOffset.UtcNow.ToString("O"),
        },
        "benefit.grant.created" or "benefit.grant.updated" or "benefit.grant.revoked" => new
        {
            id = $"bg_{Guid.NewGuid():N}",
            benefit_type = "file_download",
            customer = new { id = $"cus_{Guid.NewGuid():N}", email = "test@example.com" },
            granted_at = DateTimeOffset.UtcNow.ToString("O"),
        },
        "refund.created" or "refund.updated" => new
        {
            id = $"ref_{Guid.NewGuid():N}",
            status = "pending",
            amount = 2999,
            currency = "usd",
            order = new { id = $"ord_{Guid.NewGuid():N}" },
            created_at = DateTimeOffset.UtcNow.ToString("O"),
        },
        _ => new
        {
            id = $"evt_{Guid.NewGuid():N}",
            event_type = eventType,
            created_at = DateTimeOffset.UtcNow.ToString("O"),
        },
    };
}
