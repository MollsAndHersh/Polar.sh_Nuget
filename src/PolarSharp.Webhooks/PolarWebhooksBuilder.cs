using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.Webhooks;

/// <summary>
/// A fluent builder returned by <see cref="Extensions.WebhookBuilderExtensions.AddPolarWebhooks"/>
/// for registering PolarSharp.Webhooks services.
/// </summary>
/// <remarks>
/// Use the extension methods on this type to register handlers, toast notifications,
/// deduplication, and reconciliation without needing the full <c>PolarSharp</c> package.
/// <example>
/// Standalone usage (PolarSharp.Webhooks only — no PolarSharp core package needed):
/// <code>
/// builder.Services
///     .AddPolarWebhooks()
///     .AddWebhookHandler&lt;OrderCreatedEvent, OrderCreatedHandler&gt;()
///     .AddWebhookHandler&lt;SubscriptionActiveEvent, SubscriptionActiveHandler&gt;()
///     .AddPolarToastNotifications();
///
/// // Map the webhook endpoint directly — no UsePolarInfrastructure() required:
/// app.MapPolarWebhooks();
/// </code>
/// Full-stack usage (PolarSharp + PolarSharp.Webhooks — both packages installed):
/// <code>
/// builder.Services.AddPolarInfrastructure(builder.Configuration);
/// builder.Services
///     .AddPolarWebhooks()
///     .AddWebhookHandler&lt;OrderCreatedEvent, OrderCreatedHandler&gt;()
///     .AddPolarToastNotifications();
///
/// // UsePolarInfrastructure() auto-discovers the webhook route via keyed services:
/// app.UsePolarInfrastructure();
/// </code>
/// </example>
/// </remarks>
public sealed class PolarWebhooksBuilder(IServiceCollection services)
{
    /// <summary>
    /// Gets the underlying <see cref="IServiceCollection"/> so that additional services
    /// can be registered outside the fluent builder chain.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
