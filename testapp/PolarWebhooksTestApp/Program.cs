using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Extensions;
using PolarWebhooksTestApp.Handlers;

// ── PolarWebhooksTestApp ─────────────────────────────────────────────────────
// Standalone demo application that depends ONLY on PolarSharp.Webhooks.
// No PolarSharp core, no PolarSharp.MultiTenant — zero extra packages required.
//
// Run with: dotnet run --project testapp/PolarWebhooksTestApp
// Webhook endpoint: POST /hooks/polar
// Diagnostic endpoint: GET /
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Standalone PolarSharp.Webhooks registration.
// IServiceCollection.AddPolarWebhooks() returns PolarWebhooksBuilder for fluent chaining.
builder.Services
    .AddPolarWebhooks()
    // Orders
    .AddWebhookHandler<OrderCreatedEvent, OrderCreatedHandler>()
    .AddWebhookHandler<OrderUpdatedEvent, OrderUpdatedHandler>()
    .AddWebhookHandler<OrderPaidEvent, OrderPaidHandler>()
    .AddWebhookHandler<OrderRefundedEvent, OrderRefundedHandler>()
    // Subscriptions
    .AddWebhookHandler<SubscriptionCreatedEvent, SubscriptionCreatedHandler>()
    .AddWebhookHandler<SubscriptionActiveEvent, SubscriptionActiveHandler>()
    .AddWebhookHandler<SubscriptionUpdatedEvent, SubscriptionUpdatedHandler>()
    .AddWebhookHandler<SubscriptionCanceledEvent, SubscriptionCanceledHandler>()
    .AddWebhookHandler<SubscriptionUncanceledEvent, SubscriptionUncanceledHandler>()
    .AddWebhookHandler<SubscriptionPastDueEvent, SubscriptionPastDueHandler>()
    .AddWebhookHandler<SubscriptionRevokedEvent, SubscriptionRevokedHandler>()
    // Checkouts
    .AddWebhookHandler<CheckoutCreatedEvent, CheckoutCreatedHandler>()
    .AddWebhookHandler<CheckoutUpdatedEvent, CheckoutUpdatedHandler>()
    .AddWebhookHandler<CheckoutExpiredEvent, CheckoutExpiredHandler>()
    // Customers
    .AddWebhookHandler<CustomerCreatedEvent, CustomerCreatedHandler>()
    .AddWebhookHandler<CustomerUpdatedEvent, CustomerUpdatedHandler>()
    .AddWebhookHandler<CustomerStateChangedEvent, CustomerStateChangedHandler>()
    .AddWebhookHandler<CustomerDeletedEvent, CustomerDeletedHandler>()
    // Products
    .AddWebhookHandler<ProductCreatedEvent, ProductCreatedHandler>()
    .AddWebhookHandler<ProductUpdatedEvent, ProductUpdatedHandler>()
    // Benefits
    .AddWebhookHandler<BenefitCreatedEvent, BenefitCreatedHandler>()
    .AddWebhookHandler<BenefitUpdatedEvent, BenefitUpdatedHandler>()
    // Benefit Grants
    .AddWebhookHandler<BenefitGrantCreatedEvent, BenefitGrantCreatedHandler>()
    .AddWebhookHandler<BenefitGrantUpdatedEvent, BenefitGrantUpdatedHandler>()
    .AddWebhookHandler<BenefitGrantCycledEvent, BenefitGrantCycledHandler>()
    .AddWebhookHandler<BenefitGrantRevokedEvent, BenefitGrantRevokedHandler>()
    // Refunds
    .AddWebhookHandler<RefundCreatedEvent, RefundCreatedHandler>()
    .AddWebhookHandler<RefundUpdatedEvent, RefundUpdatedHandler>();

var app = builder.Build();

// Standalone endpoint mapping — replaces UsePolarInfrastructure() from the core package.
app.MapPolarWebhooks();

// Minimal diagnostic endpoint for manual smoke testing.
app.MapGet("/", () => Results.Ok(new
{
    Service      = "PolarWebhooksTestApp",
    Description  = "Standalone PolarSharp.Webhooks demo — no core package required.",
    WebhookPath  = "/hooks/polar",
    AllHandlers  = "28 handlers registered (Orders, Subscriptions, Checkouts, Customers, Products, Benefits, Benefit Grants, Refunds)",
    Instructions = "POST a Polar-signed webhook payload to /hooks/polar to exercise the full pipeline."
}));

app.Run();

// Expose Program as a partial class so WebApplicationFactory<Program> can reference it
// from the PolarSharp.IntegrationTests project without additional configuration.
public partial class Program { }
