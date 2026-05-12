using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Extensions;

namespace PolarSharp.Webhooks.Tests.Standalone;

/// <summary>
/// End-to-end HTTP pipeline tests for the standalone <c>PolarSharp.Webhooks</c> package.
/// Each test spins up an in-memory <see cref="TestServer"/> with only the webhook services
/// registered — no PolarSharp core, no PolarSharp.MultiTenant dependency.
/// </summary>
public sealed class StandaloneHttpPipelineTests : IAsyncLifetime
{
    private const string SecretBase64 = "dGVzdFNlY3JldEtleUZvclBvbGFy";   // "testSecretKeyForPolar"
    private const string WebhookPath  = "/hooks/polar";

    private IHost   _host   = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await BuildTestHostAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidSignedOrderCreated_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_test_order_01",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSignedSubscriptionActive_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_test_sub_01",
            eventJson: """{"type":"subscription.active","data":{"id":"sub_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidSignedRefundCreated_Returns200()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_test_ref_01",
            eventJson: """{"type":"refund.created","data":{"id":"ref_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_UnknownEventType_Returns200_WithUnknownEvent()
    {
        // Polar may add new event types in future — library should accept and acknowledge.
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_test_unknown_01",
            eventJson: """{"type":"future.event.type","data":{"id":"evt_1"}}""");

        var response = await SendWebhookAsync(body, headers);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Signature failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Post_InvalidSignature_Returns400()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"ord_1"}}""");
        var ts        = CurrentUnixTimestamp();

        var response = await SendWebhookAsync(bodyBytes, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_test_bad_sig",
            ["webhook-timestamp"] = ts,
            ["webhook-signature"] = "v1,aW52YWxpZHNpZ25hdHVyZQ=="   // wrong signature
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_TamperedBody_Returns400()
    {
        var originalJson = """{"type":"order.created","data":{"id":"ord_1"}}""";
        var ts           = CurrentUnixTimestamp();
        var sig          = ComputeSignature(SecretBase64, "wh_test_tamper_01", ts,
                               Encoding.UTF8.GetBytes(originalJson));

        // Post a different body than what was signed.
        var tamperedJson  = """{"type":"order.created","data":{"id":"TAMPERED"}}""";
        var tamperedBytes = Encoding.UTF8.GetBytes(tamperedJson);

        var response = await SendWebhookAsync(tamperedBytes, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_test_tamper_01",
            ["webhook-timestamp"] = ts,
            ["webhook-signature"] = sig
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExpiredTimestamp_Returns400()
    {
        var expiredTs = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var body      = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"ord_1"}}""");
        var sig       = ComputeSignature(SecretBase64, "wh_test_exp_01", expiredTs, body);

        var response = await SendWebhookAsync(body, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_test_exp_01",
            ["webhook-timestamp"] = expiredTs,
            ["webhook-signature"] = sig
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_MissingSignatureHeader_Returns400()
    {
        var body = Encoding.UTF8.GetBytes("""{"type":"order.created","data":{"id":"ord_1"}}""");
        var ts   = CurrentUnixTimestamp();

        var response = await SendWebhookAsync(body, new Dictionary<string, string>
        {
            ["webhook-id"]        = "wh_test_nosig",
            ["webhook-timestamp"] = ts
            // webhook-signature intentionally omitted → empty string → HMAC mismatch
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Content-Type enforcement ───────────────────────────────────────────────

    [Fact]
    public async Task Post_WrongContentType_Returns415()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_test_ct_01",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        // Override Content-Type to something invalid.
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        foreach (var (k, v) in headers)
            request.Headers.TryAddWithoutValidation(k, v);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    // ── Route correctness ─────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ToWrongPath_Returns404()
    {
        var (body, headers) = BuildSignedRequest(
            webhookId: "wh_test_route_01",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/wrong/path")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        foreach (var (k, v) in headers)
            request.Headers.TryAddWithoutValidation(k, v);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ToWebhookPath_Returns405_MethodNotAllowed()
    {
        var response = await _client.GetAsync(WebhookPath);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ── Multi-secret rotation ─────────────────────────────────────────────────

    [Fact]
    public async Task Post_SignedWithOldSecret_DuringRotation_Returns200()
    {
        const string oldSecretBase64 = "b2xkU2VjcmV0S2V5Rm9yUG9sYXI="; // "oldSecretKeyForPolar"
        using var host = await BuildTestHostAsync(
            secrets: [SecretBase64, oldSecretBase64]);
        using var client = host.GetTestClient();

        // Sign with OLD secret — should still be accepted while rotation is active.
        var (body, headers) = BuildSignedRequest(
            secretBase64: oldSecretBase64,
            webhookId: "wh_test_rotation_01",
            eventJson: """{"type":"order.created","data":{"id":"ord_1"}}""");

        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        foreach (var (k, v) in headers)
            request.Headers.TryAddWithoutValidation(k, v);

        var response = await client.SendAsync(request);
        await host.StopAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendWebhookAsync(
        byte[] body,
        Dictionary<string, string> webhookHeaders)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        foreach (var (key, value) in webhookHeaders)
            request.Headers.TryAddWithoutValidation(key, value);

        return await _client.SendAsync(request);
    }

    private static (byte[] Body, Dictionary<string, string> Headers) BuildSignedRequest(
        string webhookId,
        string eventJson,
        string? secretBase64 = null)
    {
        var secret = secretBase64 ?? SecretBase64;
        var ts     = CurrentUnixTimestamp();
        var body   = Encoding.UTF8.GetBytes(eventJson);
        var sig    = ComputeSignature(secret, webhookId, ts, body);

        return (body, new Dictionary<string, string>
        {
            ["webhook-id"]        = webhookId,
            ["webhook-timestamp"] = ts,
            ["webhook-signature"] = sig
        });
    }

    private static string ComputeSignature(string secretBase64, string webhookId, string ts, byte[] body)
    {
        var secretBytes = Convert.FromBase64String(secretBase64);
        var prefix      = Encoding.UTF8.GetBytes($"{webhookId}.{ts}.");
        var payload     = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload, prefix.Length);
        var hmac = HMACSHA256.HashData(secretBytes, payload);
        return $"v1,{Convert.ToBase64String(hmac)}";
    }

    private static string CurrentUnixTimestamp()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    private static Task<IHost> BuildTestHostAsync(IEnumerable<string>? secrets = null)
    {
        var secretList = (secrets ?? new[] { SecretBase64 }).ToList();

        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapPolarWebhooks());
                });
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    var builder = services.AddPolarWebhooks(opts =>
                    {
                        opts.Secrets      = secretList;
                        opts.Path         = WebhookPath;
                        opts.RequireHttps = false;
                    });
                    RegisterAll28Handlers(builder);
                });
            })
            .Build();

        return host.StartAsync().ContinueWith(_ => host);
    }

    private static void RegisterAll28Handlers(PolarWebhooksBuilder b)
    {
        b.AddWebhookHandler<OrderCreatedEvent,           NoOpHandler<OrderCreatedEvent>>()
         .AddWebhookHandler<OrderUpdatedEvent,           NoOpHandler<OrderUpdatedEvent>>()
         .AddWebhookHandler<OrderPaidEvent,              NoOpHandler<OrderPaidEvent>>()
         .AddWebhookHandler<OrderRefundedEvent,          NoOpHandler<OrderRefundedEvent>>()
         .AddWebhookHandler<SubscriptionCreatedEvent,    NoOpHandler<SubscriptionCreatedEvent>>()
         .AddWebhookHandler<SubscriptionActiveEvent,     NoOpHandler<SubscriptionActiveEvent>>()
         .AddWebhookHandler<SubscriptionUpdatedEvent,    NoOpHandler<SubscriptionUpdatedEvent>>()
         .AddWebhookHandler<SubscriptionCanceledEvent,   NoOpHandler<SubscriptionCanceledEvent>>()
         .AddWebhookHandler<SubscriptionUncanceledEvent, NoOpHandler<SubscriptionUncanceledEvent>>()
         .AddWebhookHandler<SubscriptionPastDueEvent,    NoOpHandler<SubscriptionPastDueEvent>>()
         .AddWebhookHandler<SubscriptionRevokedEvent,    NoOpHandler<SubscriptionRevokedEvent>>()
         .AddWebhookHandler<CheckoutCreatedEvent,        NoOpHandler<CheckoutCreatedEvent>>()
         .AddWebhookHandler<CheckoutUpdatedEvent,        NoOpHandler<CheckoutUpdatedEvent>>()
         .AddWebhookHandler<CheckoutExpiredEvent,        NoOpHandler<CheckoutExpiredEvent>>()
         .AddWebhookHandler<CustomerCreatedEvent,        NoOpHandler<CustomerCreatedEvent>>()
         .AddWebhookHandler<CustomerUpdatedEvent,        NoOpHandler<CustomerUpdatedEvent>>()
         .AddWebhookHandler<CustomerStateChangedEvent,   NoOpHandler<CustomerStateChangedEvent>>()
         .AddWebhookHandler<CustomerDeletedEvent,        NoOpHandler<CustomerDeletedEvent>>()
         .AddWebhookHandler<ProductCreatedEvent,         NoOpHandler<ProductCreatedEvent>>()
         .AddWebhookHandler<ProductUpdatedEvent,         NoOpHandler<ProductUpdatedEvent>>()
         .AddWebhookHandler<BenefitCreatedEvent,         NoOpHandler<BenefitCreatedEvent>>()
         .AddWebhookHandler<BenefitUpdatedEvent,         NoOpHandler<BenefitUpdatedEvent>>()
         .AddWebhookHandler<BenefitGrantCreatedEvent,    NoOpHandler<BenefitGrantCreatedEvent>>()
         .AddWebhookHandler<BenefitGrantUpdatedEvent,    NoOpHandler<BenefitGrantUpdatedEvent>>()
         .AddWebhookHandler<BenefitGrantCycledEvent,     NoOpHandler<BenefitGrantCycledEvent>>()
         .AddWebhookHandler<BenefitGrantRevokedEvent,    NoOpHandler<BenefitGrantRevokedEvent>>()
         .AddWebhookHandler<RefundCreatedEvent,          NoOpHandler<RefundCreatedEvent>>()
         .AddWebhookHandler<RefundUpdatedEvent,          NoOpHandler<RefundUpdatedEvent>>();
    }

    private sealed class NoOpHandler<TEvent>(ILogger<NoOpHandler<TEvent>> logger)
        : PolarWebhookHandlerBase<TEvent>(logger)
        where TEvent : WebhookEvent
    {
        protected override Task HandleCoreAsync(TEvent @event, CancellationToken ct)
            => Task.CompletedTask;
    }
}
