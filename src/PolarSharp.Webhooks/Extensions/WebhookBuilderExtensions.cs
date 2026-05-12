using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Webhooks.BackgroundQueue;
using PolarSharp.Webhooks.Dedup;
using PolarSharp.Webhooks.Events;
using PolarSharp.Webhooks.Reconciliation;
using PolarSharp.Webhooks.Toast;

namespace PolarSharp.Webhooks.Extensions;

/// <summary>
/// Extension methods for registering PolarSharp.Webhooks services and middleware.
/// </summary>
/// <remarks>
/// <para>
/// <c>PolarSharp.Webhooks</c> is a standalone NuGet package — it does not require the
/// <c>PolarSharp</c> core package. Install it independently and call
/// <see cref="AddPolarWebhooks(IServiceCollection, Action{PolarWebhookOptions}?)"/> on
/// <see cref="IServiceCollection"/>, then map the webhook endpoint with
/// <see cref="MapPolarWebhooks(IEndpointRouteBuilder)"/>.
/// </para>
/// <para>
/// When both <c>PolarSharp</c> and <c>PolarSharp.Webhooks</c> are installed,
/// <c>UsePolarInfrastructure()</c> from the core package auto-discovers and maps
/// the webhook endpoint via keyed DI services — no separate
/// <see cref="MapPolarWebhooks(IEndpointRouteBuilder)"/> call is needed.
/// </para>
/// </remarks>
public static class WebhookBuilderExtensions
{
    private const int AnomalyFailureThreshold = 10;
    private static readonly VerificationFailureTracker _failureTracker = new();

    // ── Primary entry point ───────────────────────────────────────────────────────

    /// <summary>
    /// Registers the Polar webhook receiver, HMAC validator, dispatcher, startup validator,
    /// rate limiter, and security enforcement middleware.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">Optional delegate for additional in-code configuration overrides.</param>
    /// <returns>A <see cref="PolarWebhooksBuilder"/> for fluent handler and feature registration.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// All webhook configuration is read from <c>PolarSharp:Webhooks</c> in
    /// <c>appsettings.json</c>. Use <paramref name="configure"/> only for values that
    /// cannot be expressed in configuration.
    /// <example>
    /// Standalone (PolarSharp.Webhooks only):
    /// <code>
    /// builder.Services
    ///     .AddPolarWebhooks()
    ///     .AddWebhookHandler&lt;OrderCreatedEvent, OrderCreatedHandler&gt;();
    ///
    /// app.MapPolarWebhooks();
    /// </code>
    /// Full stack (PolarSharp + PolarSharp.Webhooks):
    /// <code>
    /// builder.Services.AddPolarInfrastructure(builder.Configuration);
    /// builder.Services
    ///     .AddPolarWebhooks()
    ///     .AddWebhookHandler&lt;OrderCreatedEvent, OrderCreatedHandler&gt;();
    ///
    /// app.UsePolarInfrastructure();
    /// </code>
    /// </example>
    /// </remarks>
    public static PolarWebhooksBuilder AddPolarWebhooks(
        this IServiceCollection services,
        Action<PolarWebhookOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<PolarWebhookOptions>()
            .BindConfiguration("PolarSharp:Webhooks");

        if (configure is not null)
            services.Configure(configure);

        services.AddSingleton<WebhookValidator>();
        services.TryAddScoped<IPolarWebhookDispatcher, PolarWebhookDispatcher>();
        services.AddHostedService<PolarWebhookStartupValidator>();

        // Rate limiting — part of the shared ASP.NET Core framework, zero extra NuGet deps.
        services.AddRateLimiter(static _ => { });
        services.AddTransient<IConfigureOptions<RateLimiterOptions>, PolarWebhookRateLimiterConfigurer>();

        // Register rate-limiter activation under a well-known key so that
        // UsePolarInfrastructure() in the core package (if installed) can invoke it.
        services.AddKeyedSingleton<Action<IApplicationBuilder>>(
            "polar.webhooks.ratelimiter",
            static (sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptions<PolarWebhookOptions>>().Value;
                return opts.EnableRateLimiting
                    ? static app => app.UseRateLimiter()
                    : static _ => { };
            });

        // Register the endpoint mapping action under a well-known key so that
        // UsePolarInfrastructure() can map the route without a hard compile-time
        // dependency on this package.
        services.AddKeyedSingleton<Action<IEndpointRouteBuilder>>(
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

        return new PolarWebhooksBuilder(services);
    }

    // ── Handler registration ──────────────────────────────────────────────────────

    /// <summary>
    /// Registers a typed webhook handler for the given Polar event type.
    /// </summary>
    /// <typeparam name="TEvent">The Polar webhook event type to handle.</typeparam>
    /// <typeparam name="THandler">
    /// The handler implementation. Inherit from <see cref="PolarWebhookHandlerBase{TEvent}"/>
    /// for automatic structured logging, or implement <see cref="IPolarWebhookHandler{TEvent}"/>
    /// directly for maximum control.
    /// </typeparam>
    /// <param name="builder">The webhooks builder.</param>
    /// <param name="enqueue">
    /// When <see langword="true"/>, the event is written to a bounded
    /// <see cref="IBackgroundPolarWebhookQueue{TEvent}"/> and processed asynchronously by
    /// <see cref="PolarWebhookBackgroundService{TEvent}"/>. The webhook endpoint returns
    /// HTTP 200 immediately, preventing Polar's 30-second timeout from triggering on slow
    /// handlers. Default: <see langword="false"/> (synchronous in-request processing).
    /// </param>
    /// <returns>The same <see cref="PolarWebhooksBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static PolarWebhooksBuilder AddWebhookHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this PolarWebhooksBuilder builder,
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
                // Optional depth gauge — registers when IMeterFactory is available.
                var factory = sp.GetService<IMeterFactory>();
                if (factory is not null)
                {
                    var meter = factory.Create("PolarSharp.Webhooks");
                    meter.CreateObservableGauge(
                        "polar.channel.depth",
                        observeValue: () => queue.Count,
                        unit: "items",
                        description: "Number of events queued in the background webhook queue.");
                }
                return queue;
            });
            builder.Services.AddHostedService<PolarWebhookBackgroundService<TEvent>>();
        }

        return builder;
    }

    // ── Toast notifications ───────────────────────────────────────────────────────

    /// <summary>
    /// Enables real-time toast notifications on <see cref="IPolarToastChannel"/> for
    /// configured Polar webhook event types.
    /// </summary>
    /// <param name="builder">The webhooks builder.</param>
    /// <param name="configure">Optional delegate for in-code configuration overrides.</param>
    /// <returns>The same <see cref="PolarWebhooksBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Reads toast configuration from <c>PolarSharp:Webhooks:ToastNotifications</c>.
    /// Registers <see cref="IPolarToastChannel"/> as a <c>Singleton</c> bounded channel.
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarWebhooks()
    ///     .AddPolarToastNotifications();
    /// </code>
    /// </example>
    /// </remarks>
    public static PolarWebhooksBuilder AddPolarToastNotifications(
        this PolarWebhooksBuilder builder,
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

        builder.Services.AddSingleton<PolarToastChannelLifetime>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PolarToastChannelLifetime>());

        return builder;
    }

    // ── Deduplication ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers an in-memory webhook event deduplication store.
    /// </summary>
    /// <param name="builder">The webhooks builder.</param>
    /// <param name="configure">Optional delegate to configure dedup options.</param>
    /// <returns>The same <see cref="PolarWebhooksBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
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
    ///     .AddPolarWebhooks()
    ///     .AddPolarWebhookInMemoryDedup(opts =>
    ///     {
    ///         opts.MaxEntries = 50_000;
    ///         opts.RetentionPeriod = TimeSpan.FromHours(4);
    ///     });
    /// </code>
    /// </example>
    public static PolarWebhooksBuilder AddPolarWebhookInMemoryDedup(
        this PolarWebhooksBuilder builder,
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

    // ── Reconciliation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers the Polar webhook reconciliation service, which periodically checks
    /// the Polar Deliveries API for failed webhook deliveries and replays them through
    /// the dispatcher.
    /// </summary>
    /// <param name="builder">The webhooks builder.</param>
    /// <param name="configure">Optional delegate for code-level configuration overrides.</param>
    /// <returns>The same <see cref="PolarWebhooksBuilder"/> for fluent chaining.</returns>
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
    /// as live webhooks. Handler implementations must be idempotent — the same
    /// <c>webhook-id</c> may be dispatched more than once.
    /// </para>
    /// </remarks>
    public static PolarWebhooksBuilder AddPolarWebhookReconciliation(
        this PolarWebhooksBuilder builder,
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

    // ── Standalone middleware mapping ─────────────────────────────────────────────

    /// <summary>
    /// Maps the Polar webhook receiver endpoint directly on the endpoint route builder.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The <paramref name="endpoints"/> for further chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Use this method in standalone webhook-only applications (those that have NOT installed
    /// the <c>PolarSharp</c> core package and therefore cannot call
    /// <c>app.UsePolarInfrastructure()</c>).
    /// </para>
    /// <para>
    /// When the <c>PolarSharp</c> core package IS installed, <c>UsePolarInfrastructure()</c>
    /// discovers and maps the webhook route automatically via a keyed DI service — do NOT
    /// also call this method in that case, as it would register the route twice.
    /// </para>
    /// <example>
    /// Standalone usage:
    /// <code>
    /// var app = builder.Build();
    /// app.MapPolarWebhooks();
    /// app.Run();
    /// </code>
    /// </example>
    /// </remarks>
    public static IEndpointRouteBuilder MapPolarWebhooks(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var mapper = endpoints.ServiceProvider
            .GetKeyedService<Action<IEndpointRouteBuilder>>("polar.webhooks.mapper");

        mapper?.Invoke(endpoints);

        // Activate rate limiter middleware if enabled — must come before endpoint mapping.
        // In standalone mode the IApplicationBuilder is the WebApplication itself.
        if (endpoints is IApplicationBuilder app)
        {
            var rateLimiterAction = app.ApplicationServices
                .GetKeyedService<Action<IApplicationBuilder>>("polar.webhooks.ratelimiter");
            rateLimiterAction?.Invoke(app);
        }

        return endpoints;
    }

    // ── Webhook request handler ───────────────────────────────────────────────────

    private static async Task<IResult> HandleWebhookAsync(
        HttpContext context,
        WebhookValidator validator,
        IPolarWebhookDispatcher dispatcher,
        IOptionsMonitor<PolarWebhookOptions> optionsMonitor,
        ILogger<WebhookValidator> logger)
    {
        // Resolved from DI rather than declared as a parameter — if declared as a parameter,
        // ASP.NET Core's Minimal API binding engine attempts JSON deserialization for interface
        // types that are not registered in DI (e.g., in standalone deployments without the
        // PolarSharp.MultiTenant package), producing a 500 instead of binding null.
        var tenantScopeInitializer = context.RequestServices.GetService<IWebhookTenantScopeInitializer>();

        var opts = optionsMonitor.CurrentValue;

        // HTTPS enforcement — returns 400, NOT a redirect.
        // Polar's sender does not follow redirects; a redirect causes Polar to mark the
        // delivery as failed and retry indefinitely against the HTTP URL.
        if (opts.RequireHttps && !context.Request.IsHttps)
        {
            logger.LogWarning("Polar webhook rejected: non-HTTPS request.");
            return Results.BadRequest("HTTPS is required.");
        }

        // Content-Type enforcement.
        var contentType = context.Request.ContentType;
        if (string.IsNullOrEmpty(contentType) ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Results.StatusCode(415);
        }

        // Resolve client IP (for allowlisting and anomaly-detection hashing).
        var clientIp = GetClientIp(context, opts.UseForwardedForHeader);
        var clientIpHash = HashIp(clientIp?.ToString()) ?? "unknown";

        // IP allowlist — checked before body read to reject non-Polar senders cheaply.
        if (opts.EnableIpAllowlist && opts.AllowedSourceIpRanges.Count > 0)
        {
            if (clientIp is null || !IsIpAllowed(clientIp, opts.AllowedSourceIpRanges))
                return Results.StatusCode(403);
        }

        // Payload size limit — fast path via Content-Length header.
        var contentLength = context.Request.ContentLength;
        if (contentLength.HasValue && contentLength.Value > opts.MaxPayloadBytes)
            return Results.StatusCode(413);

        // Read body, bounded to MaxPayloadBytes.
        var webhookId        = context.Request.Headers["webhook-id"].FirstOrDefault()        ?? "";
        var webhookTimestamp = context.Request.Headers["webhook-timestamp"].FirstOrDefault() ?? "";
        var webhookSignature = context.Request.Headers["webhook-signature"].FirstOrDefault() ?? "";

        context.Request.EnableBuffering();
        using var ms = new System.IO.MemoryStream();
        await context.Request.Body.CopyToAsync(ms, context.RequestAborted).ConfigureAwait(false);
        var bodyBytes = ms.ToArray();

        if (bodyBytes.Length > opts.MaxPayloadBytes)
            return Results.StatusCode(413);

        var result = validator.Verify(webhookId, webhookTimestamp, webhookSignature, bodyBytes);

        return await result.MatchAsync<IResult>(
            async evt =>
            {
                // Populate the multi-tenant context if a scope initializer is registered.
                if (tenantScopeInitializer is not null && evt.ResolvedTenantId is not null)
                    await tenantScopeInitializer.InitializeScopeAsync(context, evt.ResolvedTenantId).ConfigureAwait(false);

                using var cts = new CancellationTokenSource();
                await dispatcher.DispatchAsync(evt, cts.Token).ConfigureAwait(false);
                return Results.Ok();
            },
            err =>
            {
                // Anomaly detection — warn when one IP accumulates >= 10 failures in 60 s.
                var failureCount = _failureTracker.RecordFailure(clientIpHash);
                if (failureCount >= AnomalyFailureThreshold)
                {
                    logger.LogWarning(
                        "PolarSharp Webhooks: Elevated verification failure rate detected. " +
                        "Source IP hash: {IpHash}. Failures: {Count} in last 60 s.",
                        clientIpHash, failureCount);
                }

                // Timing-uniform response — same body and status for every failure mode.
                return System.Threading.Tasks.Task.FromResult<IResult>(
                    Results.BadRequest(err.Message));
            }
        ).ConfigureAwait(false);
    }

    // ── IP helper methods ─────────────────────────────────────────────────────────

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
                if (IPAddress.TryParse(range, out var exactIp) && ip.Equals(exactIp))
                    return true;
            }
            else
            {
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

    /// <summary>
    /// Hashes a source IP address string for privacy-preserving logging (GDPR compliance).
    /// The raw IP is never stored; the hash prefix enables cross-entry correlation.
    /// </summary>
    /// <param name="ipAddress">The raw IP address string, or <see langword="null"/>.</param>
    /// <returns>
    /// A string of the form <c>"sha256:{firstEightHexChars}"</c>, or <c>null</c> if
    /// <paramref name="ipAddress"/> is <see langword="null"/> or empty.
    /// </returns>
    internal static string? HashIp(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return $"sha256:{Convert.ToHexString(bytes)[..8].ToLowerInvariant()}";
    }

    // ── Anomaly detection ─────────────────────────────────────────────────────────

    private sealed class VerificationFailureTracker
    {
        private readonly ConcurrentDictionary<string, long> _windows = new(StringComparer.Ordinal);

        public int RecordFailure(string ipHash)
        {
            var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            var key    = $"{ipHash}:{bucket}";
            var count  = _windows.AddOrUpdate(key, 1L, static (_, c) => c + 1);

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
}

// ── Rate limiter configurer ───────────────────────────────────────────────────

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

            // Emit optional metric — IMeterFactory may not be present in standalone deployments.
            var meterFactory = ctx.HttpContext.RequestServices.GetService<IMeterFactory>();
            if (meterFactory is not null)
            {
                var opts2        = ctx.HttpContext.RequestServices.GetService<IOptionsMonitor<PolarWebhookOptions>>();
                var useForwarded = opts2?.CurrentValue.UseForwardedForHeader ?? false;
                var rawIp        = useForwarded
                    ? ctx.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    : ctx.HttpContext.Connection.RemoteIpAddress?.ToString();
                var ipHash = WebhookBuilderExtensions.HashIp(rawIp) ?? "unknown";

                var meter = meterFactory.Create("PolarSharp.Webhooks");
                var counter = meter.CreateCounter<long>("polar.webhooks.rejected_rate_limited");
                counter.Add(1, new KeyValuePair<string, object?>("source_ip_hash", ipHash));
            }

            await ctx.HttpContext.Response.WriteAsync("Rate limit exceeded.", ct).ConfigureAwait(false);
        };
    }
}
