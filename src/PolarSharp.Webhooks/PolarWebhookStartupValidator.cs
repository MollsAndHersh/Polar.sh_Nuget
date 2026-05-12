using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.Webhooks;

/// <summary>
/// Runs a handler completeness check at startup and warns (or fails) when known Polar
/// event types have no registered handler.
/// </summary>
/// <remarks>
/// <para>
/// Executes as an <see cref="IHostedService"/> during <see cref="IHost.StartAsync"/>,
/// before the application begins serving HTTP traffic. This guarantees that coverage
/// gaps are visible at startup rather than discovered in production when an unhandled
/// event is silently discarded.
/// </para>
/// <para>
/// AOT-safe: the event-type list comes from <see cref="KnownWebhookEventTypes.All"/>, a
/// compile-time static list. No <c>Assembly.GetTypes()</c> or runtime reflection is used.
/// </para>
/// <para>
/// When <see cref="PolarWebhookOptions.FailOnMissingHandlers"/> is <see langword="true"/>,
/// this service throws <see cref="PolarWebhookConfigurationException"/> on startup if any
/// known event type has no registered handler, preventing the application from accepting
/// traffic until all handlers are registered.
/// </para>
/// </remarks>
internal sealed class PolarWebhookStartupValidator(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<PolarWebhookOptions> options,
    ILogger<PolarWebhookStartupValidator> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;
        WarnIfDefaultPath(opts);
        ValidateHandlerCoverage(opts);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void WarnIfDefaultPath(PolarWebhookOptions opts)
    {
        // The default path "/hooks/polar" is predictable — an attacker knows exactly where
        // to probe. A randomized segment adds defense-in-depth against enumeration attacks.
        // The HMAC signature is the primary security control; path obscurity is secondary.
        const string defaultPath = "/hooks/polar";
        if (string.Equals(opts.Path, defaultPath, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "PolarSharp Webhooks: Using the default webhook path '{DefaultPath}'. " +
                "For production, add a randomized segment to prevent enumeration: " +
                "PolarSharp:Webhooks:Path = /hooks/polar/<random-uuid>. " +
                "The HMAC signature is the primary security control; path obscurity adds defense-in-depth.",
                defaultPath);
        }
    }

    private void ValidateHandlerCoverage(PolarWebhookOptions opts)
    {
        // Adapters are Scoped (they wrap Scoped handlers). Create a transient scope here
        // so this Singleton service can enumerate them without a lifetime violation.
        using var scope = scopeFactory.CreateScope();
        var adapters = scope.ServiceProvider.GetServices<IWebhookHandlerAdapter>();
        var registeredEventTypes = new HashSet<Type>(adapters.Select(a => a.EventType));

        var unhandled = KnownWebhookEventTypes.All
            .Where(t => !registeredEventTypes.Contains(t))
            .ToList();

        if (unhandled.Count == 0)
        {
            logger.LogInformation(
                "PolarSharp Webhooks: all {Count} known Polar event types have registered handlers.",
                KnownWebhookEventTypes.All.Count);
            return;
        }

        if (opts.FailOnMissingHandlers)
        {
            var typeNames = unhandled
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.Ordinal);

            var message =
                $"PolarSharp Webhooks: {unhandled.Count} of {KnownWebhookEventTypes.All.Count} " +
                $"known Polar event types have no registered handler: " +
                string.Join(", ", typeNames) +
                ". Register each with AddWebhookHandler<TEvent, THandler>() or set " +
                "PolarSharp:Webhooks:FailOnMissingHandlers=false to warn instead of failing.";

            logger.LogCritical(
                "PolarSharp Webhooks startup check failed — {UnhandledCount} event types unregistered.",
                unhandled.Count);

            throw new PolarWebhookConfigurationException(message);
        }

        foreach (var t in unhandled)
        {
            logger.LogWarning(
                "PolarSharp Webhooks: No handler registered for event type '{EventTypeName}'. " +
                "If Polar delivers this event, it will be acknowledged but silently discarded. " +
                "Register a handler with AddWebhookHandler<{EventTypeName}, THandler>().",
                t.Name, t.Name);
        }

        logger.LogWarning(
            "PolarSharp Webhooks: {UnhandledCount} of {TotalCount} known Polar event types " +
            "have no registered handler. Set PolarSharp:Webhooks:WarnOnUnhandledEventTypes=false " +
            "to suppress these warnings, or PolarSharp:Webhooks:FailOnMissingHandlers=true to " +
            "fail startup instead.",
            unhandled.Count, KnownWebhookEventTypes.All.Count);
    }
}
