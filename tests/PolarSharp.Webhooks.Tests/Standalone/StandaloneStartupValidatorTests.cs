using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Extensions;

namespace PolarSharp.Webhooks.Tests.Standalone;

/// <summary>
/// Verifies <see cref="PolarWebhookStartupValidator"/> behavior when run at host startup.
/// Tests both the warn-only path and the fail-fast path.
/// </summary>
public sealed class StandaloneStartupValidatorTests
{
    // ── All handlers registered → no exception ────────────────────────────────

    [Fact]
    public async Task StartupValidator_AllHandlersRegistered_HostStartsSuccessfully()
    {
        using var host = BuildHost(allHandlers: true, failOnMissing: false);
        // Should not throw.
        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task StartupValidator_AllHandlersRegistered_WithFailOnMissing_HostStartsSuccessfully()
    {
        using var host = BuildHost(allHandlers: true, failOnMissing: true);
        // All 28 handlers present — FailOnMissingHandlers=true should NOT throw.
        await host.StartAsync();
        await host.StopAsync();
    }

    // ── Missing handlers → warn or fail ──────────────────────────────────────

    [Fact]
    public async Task StartupValidator_MissingHandlers_WarnOnly_DoesNotThrow()
    {
        // Zero handlers registered; FailOnMissingHandlers=false → warns, does not throw.
        using var host = BuildHost(allHandlers: false, failOnMissing: false);
        await host.StartAsync();   // must NOT throw
        await host.StopAsync();
    }

    [Fact]
    public async Task StartupValidator_MissingHandlers_FailOnMissingHandlers_ThrowsOnStart()
    {
        // Zero handlers registered; FailOnMissingHandlers=true → throws on startup.
        using var host = BuildHost(allHandlers: false, failOnMissing: true);

        await Assert.ThrowsAsync<PolarWebhookConfigurationException>(
            async () => await host.StartAsync());
    }

    [Fact]
    public async Task StartupValidator_PartialHandlers_FailOnMissing_ThrowsWithMissingNames()
    {
        // Only 1 of 28 handlers registered — FailOnMissingHandlers=true should throw.
        using var host = BuildHostWithPartialHandlers(failOnMissing: true);

        var ex = await Assert.ThrowsAsync<PolarWebhookConfigurationException>(
            async () => await host.StartAsync());

        // Message should mention at least one missing event type.
        Assert.Contains("Event", ex.Message);
    }

    // ── Hosted service lifetime ────────────────────────────────────────────────

    [Fact]
    public async Task StartupValidator_StopAsync_CompletesWithoutError()
    {
        using var host = BuildHost(allHandlers: true, failOnMissing: false);
        await host.StartAsync();
        // StopAsync on the validator is a no-op — must not throw.
        await host.StopAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IHost BuildHost(bool allHandlers, bool failOnMissing)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                var webhookBuilder = services.AddPolarWebhooks(opts =>
                {
                    opts.Secret             = "dGVzdA==";
                    opts.RequireHttps       = false;
                    opts.FailOnMissingHandlers = failOnMissing;
                });

                if (allHandlers)
                    RegisterAll28Handlers(webhookBuilder);
            });

        return builder.Build();
    }

    private static IHost BuildHostWithPartialHandlers(bool failOnMissing)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services
                    .AddPolarWebhooks(opts =>
                    {
                        opts.Secret                = "dGVzdA==";
                        opts.RequireHttps          = false;
                        opts.FailOnMissingHandlers = failOnMissing;
                    })
                    .AddWebhookHandler<OrderCreatedEvent, NoOpHandler<OrderCreatedEvent>>();
                // 27 other handlers intentionally NOT registered.
            });

        return builder.Build();
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
