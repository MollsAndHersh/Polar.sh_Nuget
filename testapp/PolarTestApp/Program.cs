using PolarSharp.Extensions;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Extensions;
using PolarSharp.MultiTenant.Extensions;
using PolarTestApp.Endpoints;
using PolarTestApp.Handlers;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Polar sandbox token wiring (SITE-MASTER, NOT per-tenant) ────────────────
// The site-master Polar OAT (the SaaS app's own credential, used for cross-cutting
// operations — NOT for tenant-scoped operations) is loaded from the
// POLAR_SANDBOX_TOKEN environment variable rather than hardcoded in
// appsettings.json. Sources, in increasing priority:
//
//   1. appsettings.json -> "PolarSharp:AccessToken" (left empty in source control;
//      placeholder only).
//   2. dotnet user-secrets (Development env only) -> the
//      <UserSecretsId>PolarTestApp-secrets</UserSecretsId> in the csproj. Set via
//      `dotnet user-secrets set "PolarSharp:AccessToken" "polar_oat_..."`.
//   3. POLAR_SANDBOX_TOKEN environment variable (this bridge). Loaded by direnv
//      from .env locally; injected from the GitHub Actions secret in CI. This is
//      the recommended path for both local dev (one secret in one place) and CI.
//
// Tenant tokens (PolarSharp:MultiTenant:Tenants[N].PolarAccessToken) are
// intentionally a separate concern — each tenant has their own Polar org and
// their own OAT, supplied per-tenant via host-specific config (database row,
// per-tenant user-secret, etc.). This bridge does NOT touch those.
var sandboxToken = Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");
if (!string.IsNullOrWhiteSpace(sandboxToken))
{
    builder.Configuration["PolarSharp:AccessToken"] = sandboxToken;
}

// Core infrastructure + multi-tenant (PolarSharp + PolarSharp.MultiTenant)
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant();

// Webhook handlers (PolarSharp.Webhooks — standalone; no core package required)
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

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseRequestLocalization(opts =>
    opts.SetDefaultCulture("en-US")
        .AddSupportedCultures("en-US", "es-MX")
        .AddSupportedUICultures("en-US", "es-MX"));

app.UsePolarInfrastructure();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapOrderEndpoints()
   .MapSubscriptionEndpoints()
   .MapCustomerEndpoints()
   .MapProductEndpoints()
   .MapCheckoutEndpoints()
   .MapBenefitEndpoints()
   .MapLicenseKeyEndpoints()
   .MapMeterEndpoints()
   .MapDiscountEndpoints()
   .MapRefundEndpoints()
   .MapWebhookSimulatorEndpoints()
   .MapMultiTenantEndpoints()
   .MapLocalizationEndpoints()
   .MapDiagnosticsEndpoints()
   .MapCustomerPortalEndpoints();

app.Run();
