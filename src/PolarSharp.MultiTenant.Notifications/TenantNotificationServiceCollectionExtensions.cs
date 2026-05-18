using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.Notifications.Channels;

namespace PolarSharp.MultiTenant.Notifications;

/// <summary>DI registration helpers for the opt-in tenant lifecycle notification dispatcher.</summary>
/// <remarks>
/// <para>
/// Registers <see cref="ITenantStatusNotifier"/>, the three channels (<see cref="SendGridEmailChannel"/>,
/// <see cref="TwilioSmsChannel"/>, <see cref="WebhookChannel"/>), the corresponding named
/// <see cref="HttpClient"/> instances, the options validator, and the MediatR
/// <see cref="TenantStatusChangedNotificationHandler"/>.
/// </para>
/// <para>
/// MediatR de-duplicates assembly scans, so calling this after
/// <c>AddPolarTenantLifecycle</c> (which scans <c>PolarSharp.MultiTenant</c>) is safe — only
/// this package's handler is added.
/// </para>
/// </remarks>
public static class TenantNotificationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the opt-in tenant lifecycle notification dispatcher to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The application configuration root. Bound to
    /// <see cref="TenantNotificationOptions.SectionName"/>
    /// (<c>PolarSharp:MultiTenant:Notifications</c>).
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services
    ///     .AddPolarMultiTenant(...)
    ///     .AddPolarTenantLifecycle(builder.Configuration)
    ///     .AddPolarMultiTenantNotifications(builder.Configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddPolarMultiTenantNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TenantNotificationOptions>(
            configuration.GetSection(TenantNotificationOptions.SectionName));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TenantNotificationOptions>, TenantNotificationOptionsValidator>());

        services.AddHttpClient(SendGridEmailChannel.HttpClientName);
        services.AddHttpClient(TwilioSmsChannel.HttpClientName);
        services.AddHttpClient(WebhookChannel.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<TenantNotificationOptions>>().CurrentValue;
            // Clamp to the validator's [1, 300] range so an unbound/invalid config never produces
            // an out-of-range TimeSpan here.
            var seconds = Math.Clamp(opts.Webhook.TimeoutSeconds, 1, 300);
            client.Timeout = TimeSpan.FromSeconds(seconds);
        });

        services.TryAddSingleton<IEmailChannel, SendGridEmailChannel>();
        services.TryAddSingleton<ISmsChannel, TwilioSmsChannel>();
        services.TryAddSingleton<IWebhookChannel, WebhookChannel>();
        services.TryAddSingleton<ITenantStatusNotifier, DefaultTenantStatusNotifier>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
            typeof(TenantStatusChangedNotificationHandler).Assembly));

        return services;
    }
}
