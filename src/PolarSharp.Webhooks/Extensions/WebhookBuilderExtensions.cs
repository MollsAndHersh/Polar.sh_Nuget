using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Telemetry;
using PolarSharp.Webhooks.BackgroundQueue;
using PolarSharp.Webhooks.Dedup;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Reconciliation;
using PolarSharp.Webhooks.Toast;

namespace PolarSharp.Webhooks.Extensions;

/// <summary>
/// Extension methods on <see cref="PolarInfrastructureBuilder"/> for registering
/// PolarSharp.Webhooks services.
/// </summary>
public static class WebhookBuilderExtensions
{
    private const int AnomalyFailureThreshold = 10;
    private static readonly VerificationFailureTracker _failureTracker = new();

    /// <summary>
    /// Registers the Polar webhook receiver, HMAC validator, dispatcher, startup validator,
    /// rate limiter, and security enforcement middleware.
    /// </summary>
    /// <param name="builder">The infrastructure builder returned by <c>AddPolarInfrastructure</c>.</param>
    /// <param name="configure">Optional delegate for additional in-code configuration overrides.</param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// All webhook configuration is read from <c>PolarSharp:Webhooks</c> in
    /// <c>appsettings.json</c>. Use <paramref name="configure"/> only for values that
    /// cannot be expressed in configuration (e.g., conditional logic).
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarWebhooks()
    ///     .AddWebhookHandler&lt;OrderCreatedEvent, OrderCreatedHandler&gt;()
    ///     .AddWebhookHandler&lt;SubscriptionActiveEvent, SubscriptionActiveHandler&gt;();
    /// </code>
    /// </example>
    /// </remarks>
    public static PolarInfrastructureBuilder AddPolarWebhooks(
        this PolarInfrastructureBuilder builder,
        Action<PolarWebhookOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<PolarWebhookOptions>()
            .BindConfiguration("PolarSharp:Webhooks");

        if (configure is not null)
            builder.Services.Configure(configure);

        builder.Services.AddSingleton<WebhookValidator>();
        builder.Services.TryAddScoped<IPolarWebhookDispatcher, PolarWebhookDispatcher>();
        builder.Services.AddHostedService<PolarWebhookStartupValidator>();

        // Register ASP.NET Core rate limiting infrastructure (zero external dependencies —
        // Microsoft.AspNetCore.RateLimiting is part of the shared framework).
        // The actual permit limit and window are read from PolarWebhookOptions at runtime
        // via the IConfigureOptions<RateLimiterOptions> below.
        builder.Services.AddRateLimiter(static _ => { });
        builder.Services.AddTransient<IConfigureOptions<RateLimiterOptions>, PolarWebhookRateLimiterConfigurer>();

        // Rate limiter middleware activation — invoked by UsePolarInfrastructure() BEFORE
        // endpoint mapping so that RequireRateLimiting on the webhook route takes effect.
        builder.Services.AddKeyedSingleton<Action<IApplicationBuilder>>(
            "polar.webhooks.ratelimiter",
            static (sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
                // Only activate the middleware if rate limiting is enabled; otherwise no-op.
                return opts.EnableRateLimiting
                    ? static app => app.UseRateLimiter()
                    : static _ => { };
            });

        // Register the endpoint mapping action under a well-known key so that
        // UsePolarInfrastructure() (in core) can invoke it without a hard dependency
        // on this package. Action<IEndpointRouteBuilder> is a public BCL type.
        builder.Services.AddKeyedSingleton<Action<IEndpointRouteBuilder>>(
            "polar.webhooks.mapper",
            static (sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
                var path = opts.Path;
                return endpoints =>
                {
                    var mapping = endpoints.MapPost(path, HandleWebhookAsync)
                        .WithName("PolarWebhookReceiver")
                        .ExcludeFromDescription();

                    if (opts.EnableRateLimiting)
                        mapping.RequireRateLimiting("polar-webhooks");
                };
            });

        SetMarkerFlag(builder.Services, m => m.WebhooksRegistered = true);

        return builder;
    }

    /// <summary>
    /// Registers a typed webhook handler for the given Polar event type.
    /// </summary>
    /// <typeparam name="TEvent">The Polar webhook event type to handle.</typeparam>
    /// <typeparam name="THandler">
    /// The handler implementation. Inherit from <see cref="PolarWebhookHandlerBase{TEvent}"/>
    /// for automatic structured logging, or implement <see cref="IPolarWebhookHandler{TEvent}"/>
    /// directly for maximum control.
    /// </typeparam>
    /// <param name="builder">The infrastructure builder.</param>
    /// <param name="enqueue">
    /// When <see langword="true"/>, the event is written to a bounded
    /// <see cref="IBackgroundPolarWebhookQueue{TEvent}"/> and processed asynchronously by
    /// <see cref="PolarWebhookBackgroundService{TEvent}"/>. The webhook endpoint returns
    /// HTTP 200 immediately, preventing Polar's 30-second timeout from triggering on slow
    /// handlers. Default: <see langword="false"/> (synchronous processing).
    /// </param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static PolarInfrastructureBuilder AddWebhookHandler<TEvent, THandler>(
        this PolarInfrastructureBuilder builder,
        bool enqueue = false)
        where TEvent : WebhookEvent
        where THandler : class, IPolarWebhookHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<IPolarWebhookHandler<TEvent>, THandler>();
        builder.Services.AddScoped<IWebhookHandlerAdapter>(sp =>
            new WebhookHandlerAdapter<TEvent>(sp.GetRequiredService<IPolarWebhookHandler<TEvent>>()));

        if (enqueue)
        {
            builder.Services.AddSingleton<IBackgroundPolarWebhookQueue<TEvent>>(sp =>
            {
                var queue = new PolarWebhookBackgroundQueue<TEvent>(capacity: 1000);
                // Register depth gauge — polar.channel.depth with channel_name tag.
                var polarMeter = sp.GetService<PolarMeter>();
                polarMeter?.RegisterChannelDepthProvider(
                    $"webhook-queue-{typeof(TEvent).Name}",
                    () => queue.Count);
                return queue;
            });
            builder.Services.AddHostedService<PolarWebhookBackgroundService<TEvent>>();
        }

        return builder;
    }

    /// <summary>
    /// Enables real-time toast notifications on <see cref="IPolarToastChannel"/> for
    /// configured Polar webhook event types.
    /// </summary>
    /// <param name="builder">The infrastructure builder.</param>
    /// <param name="configure">Optional delegate for in-code configuration overrides.</param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Reads toast configuration from <c>PolarSharp:Webhooks:ToastNotifications</c>.
    /// Registers <see cref="IPolarToastChannel"/> as a <c>Singleton</c> bounded channel.
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarWebhooks()
    ///     .AddPolarToastNotifications();
    /// </code>
    /// </example>
    /// </remarks>
    public static PolarInfrastructureBuilder AddPolarToastNotifications(
        this PolarInfrastructureBuilder builder,
        Action<PolarToastOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure<PolarWebhookOptions>(opts =>
            {
                opts.ToastNotifications ??= new PolarToastOptions();
                configure(opts.ToastNotifications);
            });
        }

        builder.Services.TryAddSingleton<IPolarToastChannel>(sp =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<PolarWebhookOptions>>().CurrentValue;
            var capacity = opts.ToastNotifications?.ChannelCapacity ?? 100;
            return new PolarToastChannel(capacity);
        });

        // Register shutdown service that completes the writer on host stop and wires the depth gauge.
        builder.Services.AddSingleton<PolarToastChannelLifetime>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PolarToastChannelLifetime>());

        SetMarkerFlag(builder.Services, m => m.ToastNotificationsRegistered = true);

        return builder;
    }

    /// <summary>
    /// Registers an in-memory webhook event deduplication store.
    /// </summary>
    /// <param name="builder">The <see cref="PolarInfrastructureBuilder"/> to extend.</param>
    /// <param name="configure">Optional delegate to configure dedup options.</param>
    /// <returns>The <paramref name="builder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers <see cref="IWebhookIdempotencyStore"/> backed by a bounded
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
    /// with automatic LRU eviction and periodic pruning.
    /// </para>
    /// <para>
    /// <strong>Single-process only</strong> — this store is not shared across multiple instances.
    /// For multi-replica deployments, implement <see cref="IWebhookIdempotencyStore"/> backed
    /// by Redis, SQL Server, or another distributed store.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarWebhooks()
    ///     .AddPolarWebhookInMemoryDedup(opts =>
    ///     {
    ///         opts.MaxEntries = 50_000;
    ///         opts.RetentionPeriod = TimeSpan.FromHours(4);
    ///     });
    /// </code>
    /// </example>
    public static PolarInfrastructureBuilder AddPolarWebhookInMemoryDedup(
        this PolarInfrastructureBuilder builder,
        Action<PolarWebhookInMemoryDedupOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var opts = new PolarWebhookInMemoryDedupOptions();
        configure?.Invoke(opts);

        builder.Services.AddSingleton(opts);
        builder.Services.AddSingleton<IWebhookIdempotencyStore, WebhookInMemoryDedupService>();
        builder.Services.AddHostedService<WebhookInMemoryDedupService>(sp =>
            (WebhookInMemoryDedupService)sp.GetRequiredService<IWebhookIdempotencyStore>());

        return builder;
    }

    // ── Marker mutation ──────────────────────────────────────────────────────────

    private static void SetMarkerFlag(
        IServiceCollection services,
        Action<PolarInfrastructureMarker> configure)
    {
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(PolarInfrastructureMarker)
              && d.ImplementationInstance is PolarInfrastructureMarker);

        if (descriptor?.ImplementationInstance is PolarInfrastructureMarker marker)
            configure(marker);
    }

    // ── Webhook request handler ──────────────────────────────────────────────────

    private static async Task<IResult> HandleWebhookAsync(
        HttpContext context,
        WebhookValidator validator,
        IPolarWebhookDispatcher dispatcher,
        IOptionsMonitor<PolarWebhookOptions> optionsMonitor,
        PolarMeter meter,
        ILogger<WebhookValidator> logger)
    {
        var opts = optionsMonitor.CurrentValue;

        // HTTPS enforcement — returns 400, NOT a redirect.
        // Polar's sender does not follow redirects; a redirect would cause Polar to mark
        // the delivery as failed and retry indefinitely against the HTTP URL.
        if (opts.RequireHttps && !context.Request.IsHttps)
        {
            logger.LogWarning("Polar webhook rejected: non-HTTPS request.");
            return Results.BadRequest("HTTPS is required.");
        }

        // Content-Type enforcement — prevents unexpected content types from reaching HMAC verify.
        var contentType = context.Request.ContentType;
        if (string.IsNullOrEmpty(contentType) ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Results.StatusCode(415);
        }

        // Resolve client IP (for allowlisting and anomaly-detection hashing).
        var clientIp = GetClientIp(context, opts.UseForwardedForHeader);
        var clientIpHash = PolarPiiRedactor.HashIp(clientIp?.ToString()) ?? "unknown";

        // IP allowlist — checked before body read to reject non-Polar senders cheaply.
        if (opts.EnableIpAllowlist && opts.AllowedSourceIpRanges.Count > 0)
        {
            if (clientIp is null || !IsIpAllowed(clientIp, opts.AllowedSourceIpRanges))
            {
                meter.IncrementWebhookRejectedIpNotAllowed(clientIpHash);
                return Results.StatusCode(403);
            }
        }

        // Payload size limit — fast path via Content-Length header (avoids body allocation).
        var contentLength = context.Request.ContentLength;
        if (contentLength.HasValue && contentLength.Value > opts.MaxPayloadBytes)
        {
            meter.IncrementWebhookRejectedTooLarge();
            return Results.StatusCode(413);
        }

        // Read body, bounded to MaxPayloadBytes to guard against missing Content-Length.
        var webhookId        = context.Request.Headers["webhook-id"].FirstOrDefault()        ?? "";
        var webhookTimestamp = context.Request.Headers["webhook-timestamp"].FirstOrDefault() ?? "";
        var webhookSignature = context.Request.Headers["webhook-signature"].FirstOrDefault() ?? "";

        context.Request.EnableBuffering();
        using var ms = new System.IO.MemoryStream();
        await context.Request.Body.CopyToAsync(ms, context.RequestAborted).ConfigureAwait(false);
        var bodyBytes = ms.ToArray();

        // Payload size limit — check actual body length when Content-Length is absent.
        if (bodyBytes.Length > opts.MaxPayloadBytes)
        {
            meter.IncrementWebhookRejectedTooLarge();
            return Results.StatusCode(413);
        }

        var result = validator.Verify(webhookId, webhookTimestamp, webhookSignature, bodyBytes);

        return await result.MatchAsync<IResult>(
            async evt =>
            {
                using var cts = new CancellationTokenSource();
                await dispatcher.DispatchAsync(evt, cts.Token).ConfigureAwait(false);
                return Results.Ok();
            },
            err =>
            {
                // Record security metrics for the failure.
                meter.IncrementWebhookRejectedInvalidSignature(clientIpHash);
                meter.RecordVerificationFailure(clientIpHash);

                // Anomaly detection — emit warning and set suspicious-activity gauge when
                // a single IP hash accumulates >= 10 failures within a 60-second window.
                var failureCount = _failureTracker.RecordFailure(clientIpHash);
                if (failureCount >= AnomalyFailureThreshold)
                {
                    meter.SignalSuspiciousActivity();
                    logger.LogWarning(
                        "PolarSharp Webhooks: Elevated verification failure rate detected. " +
                        "Source IP hash: {IpHash}. Failures: {Count} in last 60 s. " +
                        "Automatic rate limiter already applied; no additional action needed. " +
                        "If unexpected, investigate the source IP.",
                        clientIpHash, failureCount);
                }

                // Timing-uniform response — same body and status for every failure mode.
                return System.Threading.Tasks.Task.FromResult<IResult>(
                    Results.BadRequest(err.Message));
            }
        ).ConfigureAwait(false);
    }

    // ── IP helper methods ────────────────────────────────────────────────────────

    private static IPAddress? GetClientIp(HttpContext context, bool useForwardedFor)
    {
        if (useForwardedFor)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                var firstIp = forwarded.Split(',')[0].Trim();
                if (IPAddress.TryParse(firstIp, out var parsed))
                    return parsed;
            }
        }
        return context.Connection.RemoteIpAddress;
    }

    private static bool IsIpAllowed(IPAddress ip, List<string> allowedRanges)
    {
        foreach (var range in allowedRanges)
        {
            var slash = range.IndexOf('/', StringComparison.Ordinal);
            if (slash < 0)
            {
                // Exact IP match
                if (IPAddress.TryParse(range, out var exactIp) && ip.Equals(exactIp))
                    return true;
            }
            else
            {
                // CIDR notation — parse prefix length
                if (!int.TryParse(range.AsSpan(slash + 1), out var prefix))
                    continue;
                if (IsInCidr(ip, range[..slash], prefix))
                    return true;
            }
        }
        return false;
    }

    private static bool IsInCidr(IPAddress ip, string networkStr, int prefixLength)
    {
        if (!IPAddress.TryParse(networkStr, out var network))
            return false;

        // Map both to IPv4 for comparison; handles IPv4-mapped IPv6 addresses from Kestrel.
        var ipBytes      = ip.MapToIPv4().GetAddressBytes();
        var networkBytes = network.MapToIPv4().GetAddressBytes();

        if (ipBytes.Length != networkBytes.Length)
            return false;

        for (var i = 0; i < 4; i++)
        {
            var bitsInByte = Math.Clamp(prefixLength - (i * 8), 0, 8);
            var mask = bitsInByte == 0 ? (byte)0x00
                     : bitsInByte >= 8 ? (byte)0xFF
                     : (byte)(0xFF << (8 - bitsInByte));

            if ((ipBytes[i] & mask) != (networkBytes[i] & mask))
                return false;
        }
        return true;
    }

    // ── Anomaly detection — per-IP failure tracking ──────────────────────────────

    // Tracks webhook verification failures per IP hash in 60-second buckets.
    // Used to detect brute-force HMAC attacks and trigger the suspicious-activity gauge.
    private sealed class VerificationFailureTracker
    {
        // Key: "{ipHash}:{60-second-window-bucket}", Value: failure count within that bucket.
        private readonly ConcurrentDictionary<string, long> _windows = new(StringComparer.Ordinal);

        public int RecordFailure(string ipHash)
        {
            var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            var key    = $"{ipHash}:{bucket}";
            var count  = _windows.AddOrUpdate(key, 1L, static (_, c) => c + 1);

            // Clean up stale buckets on each new bucket entry to bound memory usage.
            if (count == 1)
                CleanupOldBuckets(bucket);

            return (int)Math.Min(count, int.MaxValue);
        }

        private void CleanupOldBuckets(long currentBucket)
        {
            foreach (var existingKey in _windows.Keys)
            {
                var colonIdx = existingKey.LastIndexOf(':');
                if (colonIdx >= 0 &&
                    long.TryParse(existingKey.AsSpan(colonIdx + 1), out var keyBucket) &&
                    keyBucket < currentBucket - 1)
                    _windows.TryRemove(existingKey, out _);
            }
        }
    }

    // ── Reconciliation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers the Polar webhook reconciliation service, which periodically checks
    /// the Polar Deliveries API for failed webhook deliveries and replays them through
    /// the dispatcher.
    /// </summary>
    /// <param name="builder">The infrastructure builder.</param>
    /// <param name="configure">Optional delegate for code-level configuration overrides.</param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// All settings are read from <c>PolarSharp:Webhooks:Reconciliation</c>. Defaults:
    /// <c>Enabled=true</c>, <c>IntervalMinutes=15</c>, <c>MaxLookbackHours=24</c>,
    /// <c>Storage=File</c>, <c>FilePath=polarsharp-checkpoint.json</c>.
    /// </para>
    /// <para>
    /// Replayed events flow through the same <see cref="IPolarWebhookDispatcher"/> pipeline
    /// as live webhooks. Handler implementations must be idempotent.
    /// </para>
    /// <para>
    /// For multi-instance deployments, implement <see cref="IReconciliationCheckpointStore"/>
    /// backed by Redis or SQL and configure <c>Storage=Custom</c>.
    /// </para>
    /// </remarks>
    public static PolarInfrastructureBuilder AddPolarWebhookReconciliation(
        this PolarInfrastructureBuilder builder,
        Action<PolarReconciliationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<PolarReconciliationOptions>()
            .BindConfiguration("PolarSharp:Webhooks:Reconciliation");

        if (configure is not null)
            builder.Services.Configure(configure);

        builder.Services.TryAddSingleton<IReconciliationCheckpointStore, FileCheckpointStore>();
        builder.Services.AddHostedService<PolarWebhookReconciler>();

        return builder;
    }
}

// ── Rate limiter configurer ───────────────────────────────────────────────────

// Reads PolarWebhookOptions at DI resolution time (after the container is built)
// to configure the ASP.NET Core rate limiter with the correct permit limit and window.
// Using IConfigureOptions<RateLimiterOptions> avoids BuildServiceProvider() anti-pattern.
internal sealed class PolarWebhookRateLimiterConfigurer(IOptions<PolarWebhookOptions> polarOpts)
    : IConfigureOptions<RateLimiterOptions>
{
    /// <inheritdoc/>
    public void Configure(RateLimiterOptions options)
    {
        var webhookOpts = polarOpts.Value;

        options.AddFixedWindowLimiter("polar-webhooks", limiterOpts =>
        {
            limiterOpts.PermitLimit          = webhookOpts.RateLimitPermitLimit;
            limiterOpts.Window               = TimeSpan.FromSeconds(webhookOpts.RateLimitWindowSeconds);
            limiterOpts.QueueLimit           = 0;
            limiterOpts.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        });

        options.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.Response.StatusCode = 429;

            // Emit metric for rate-limited requests. IP is hashed for GDPR compliance.
            var meter = ctx.HttpContext.RequestServices.GetService<PolarMeter>();
            if (meter is not null)
            {
                var opts2     = ctx.HttpContext.RequestServices.GetService<IOptionsMonitor<PolarWebhookOptions>>();
                var useForwarded = opts2?.CurrentValue.UseForwardedForHeader ?? false;
                var rawIp     = useForwarded
                    ? ctx.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    : ctx.HttpContext.Connection.RemoteIpAddress?.ToString();
                var ipHash    = PolarPiiRedactor.HashIp(rawIp) ?? "unknown";
                meter.IncrementWebhookRateLimited(ipHash);
            }

            await ctx.HttpContext.Response.WriteAsync("Rate limit exceeded.", ct).ConfigureAwait(false);
        };
    }
}
