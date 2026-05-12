using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Extensions;

namespace PolarSharp.Webhooks.Tests.Standalone;

/// <summary>
/// Verifies that <see cref="WebhookBuilderExtensions.AddPolarWebhooks"/> registers all
/// required services when used in standalone mode (no PolarSharp core package installed).
/// </summary>
public sealed class StandaloneRegistrationTests
{
    // ── Core service registration ─────────────────────────────────────────────

    [Fact]
    public void AddPolarWebhooks_RegistersWebhookValidator()
    {
        using var sp = BuildMinimalProvider();
        Assert.NotNull(sp.GetService<WebhookValidator>());
    }

    [Fact]
    public void AddPolarWebhooks_RegistersIPolarWebhookDispatcher()
    {
        using var sp = BuildMinimalProvider();
        using var scope = sp.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarWebhookDispatcher>());
    }

    [Fact]
    public void AddPolarWebhooks_BindsPolarWebhookOptions()
    {
        using var sp = BuildMinimalProvider(secret: "whsec_dGVzdA==", path: "/hooks/test");
        var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
        Assert.Equal("whsec_dGVzdA==", opts.Secret);
        Assert.Equal("/hooks/test", opts.Path);
    }

    [Fact]
    public void AddPolarWebhooks_ReturnsPolarWebhooksBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddPolarWebhooks();
        Assert.IsType<PolarWebhooksBuilder>(builder);
    }

    [Fact]
    public void AddPolarWebhooks_BuilderExposesServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddPolarWebhooks();
        Assert.Same(services, builder.Services);
    }

    // ── Handler registration ──────────────────────────────────────────────────

    [Fact]
    public void AddWebhookHandler_RegistersTypedHandlerInterface()
    {
        using var sp = BuildProviderWithSingleHandler<OrderCreatedEvent, NoOpHandler<OrderCreatedEvent>>();
        using var scope = sp.CreateScope();
        var handler = scope.ServiceProvider.GetService<IPolarWebhookHandler<OrderCreatedEvent>>();
        Assert.NotNull(handler);
    }

    [Fact]
    public void AddWebhookHandler_RegistersAdapterForStartupValidation()
    {
        using var sp = BuildProviderWithSingleHandler<OrderCreatedEvent, NoOpHandler<OrderCreatedEvent>>();
        using var scope = sp.CreateScope();
        var adapters = scope.ServiceProvider.GetServices<IWebhookHandlerAdapter>().ToList();
        Assert.Contains(adapters, a => a.EventType == typeof(OrderCreatedEvent));
    }

    [Fact]
    public void AddWebhookHandler_HandlerIsScoped()
    {
        using var sp = BuildProviderWithSingleHandler<OrderCreatedEvent, NoOpHandler<OrderCreatedEvent>>();

        // Each scope gets its own instance.
        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var h1 = scope1.ServiceProvider.GetRequiredService<IPolarWebhookHandler<OrderCreatedEvent>>();
        var h2 = scope2.ServiceProvider.GetRequiredService<IPolarWebhookHandler<OrderCreatedEvent>>();
        Assert.NotSame(h1, h2);
    }

    [Fact]
    public void AddWebhookHandler_TwoHandlers_BothRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services
            .AddPolarWebhooks(opts => { opts.Secret = "dGVzdA=="; opts.RequireHttps = false; })
            .AddWebhookHandler<OrderCreatedEvent, NoOpHandler<OrderCreatedEvent>>()
            .AddWebhookHandler<SubscriptionActiveEvent, NoOpHandler<SubscriptionActiveEvent>>();

        using var sp    = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarWebhookHandler<OrderCreatedEvent>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IPolarWebhookHandler<SubscriptionActiveEvent>>());
    }

    [Fact]
    public void AddWebhookHandler_All28Handlers_AllAdaptersRegistered()
    {
        using var sp = BuildProviderWithAll28Handlers();
        using var scope = sp.CreateScope();
        var adapters = scope.ServiceProvider.GetServices<IWebhookHandlerAdapter>().ToList();

        var registeredTypes  = adapters.Select(a => a.EventType).ToHashSet();
        var missingTypeNames = KnownWebhookEventTypes.All
            .Where(t => !registeredTypes.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(missingTypeNames);
    }

    // ── Keyed DI registration (standalone route mapping) ─────────────────────

    [Fact]
    public void AddPolarWebhooks_RegistersKeyedRouteMapper()
    {
        using var sp = BuildMinimalProvider();
        var mapper = sp.GetKeyedService<Action<IEndpointRouteBuilder>>("polar.webhooks.mapper");
        Assert.NotNull(mapper);
    }

    [Fact]
    public void AddPolarWebhooks_RegistersKeyedRateLimiterActivator()
    {
        using var sp = BuildMinimalProvider();
        var activator = sp.GetKeyedService<Action<IApplicationBuilder>>("polar.webhooks.ratelimiter");
        Assert.NotNull(activator);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ServiceProvider BuildMinimalProvider(
        string secret = "dGVzdA==",
        string path   = "/hooks/polar")
    {
        var services = new ServiceCollection();
        // IConfiguration must be registered; BindConfiguration("PolarSharp:Webhooks") inside
        // AddPolarWebhooks requires it at resolve time even when values come from the configure delegate.
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddPolarWebhooks(opts =>
        {
            opts.Secret       = secret;
            opts.Path         = path;
            opts.RequireHttps = false;
        });
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildProviderWithSingleHandler<TEvent, THandler>()
        where TEvent   : WebhookEvent
        where THandler : class, IPolarWebhookHandler<TEvent>
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services
            .AddPolarWebhooks(opts => { opts.Secret = "dGVzdA=="; opts.RequireHttps = false; })
            .AddWebhookHandler<TEvent, THandler>();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildProviderWithAll28Handlers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services
            .AddPolarWebhooks(opts => { opts.Secret = "dGVzdA=="; opts.RequireHttps = false; })
            .AddWebhookHandler<OrderCreatedEvent,           NoOpHandler<OrderCreatedEvent>>()
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
        return services.BuildServiceProvider();
    }

    // ── Generic no-op handler stub ────────────────────────────────────────────

    private sealed class NoOpHandler<TEvent>(ILogger<NoOpHandler<TEvent>> logger)
        : PolarWebhookHandlerBase<TEvent>(logger)
        where TEvent : WebhookEvent
    {
        protected override Task HandleCoreAsync(TEvent @event, CancellationToken ct)
            => Task.CompletedTask;
    }
}
