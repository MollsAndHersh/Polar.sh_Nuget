using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using PolarSharp.Auth;
using PolarSharp.Connection;
using PolarSharp.Health;
using PolarSharp.Idempotency;
using PolarSharp.Localization;
using PolarSharp.Telemetry;
using PolarSharp.Versioning;

namespace PolarSharp.Extensions;

/// <summary>
/// Extension methods for registering PolarSharp core services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "PolarSharp";

    /// <summary>
    /// Registers all PolarSharp core services with the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">
    /// The application configuration. PolarSharp reads the <c>PolarSharp</c> section;
    /// see <see cref="PolarOptions"/> for the full schema.
    /// </param>
    /// <returns>
    /// A <see cref="PolarInfrastructureBuilder"/> that optional packages
    /// (<c>PolarSharp.Webhooks</c>, <c>PolarSharp.MultiTenant</c>) extend via their own
    /// <c>AddPolar*</c> methods.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// Minimal single-tenant registration:
    /// <code>
    /// builder.Services.AddPolarInfrastructure(builder.Configuration);
    /// </code>
    /// Full stack with webhooks and multi-tenancy:
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarWebhooks()
    ///     .AddPolarMultiTenant();
    /// </code>
    /// </example>
    /// <remarks>
    /// Registers the following services internally (not exhaustive):
    /// <list type="number">
    ///   <item><c>IOptions&lt;PolarOptions&gt;</c> bound from <c>PolarSharp</c> config section.</item>
    ///   <item><c>PolarOptionsValidator</c> as <c>IValidateOptions&lt;PolarOptions&gt;</c> + <c>ValidateOnStart()</c>.</item>
    ///   <item>Named <c>HttpClient("PolarSharp")</c> with the full handler pipeline.</item>
    ///   <item><c>PolarClient</c> as a Singleton.</item>
    ///   <item><c>PolarHealthCheck</c> with tag <c>"polar"</c>.</item>
    ///   <item><c>PolarActivitySource</c> and <c>PolarMeter</c> singletons for OpenTelemetry.</item>
    ///   <item><c>IPolarLocalizer</c> via <c>TryAddSingleton</c> — host override takes precedence.</item>
    /// </list>
    /// Calling <c>AddLocalization()</c> again in the host app is safe (idempotent).
    /// </remarks>
    public static PolarInfrastructureBuilder AddPolarInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 1. Bind + validate options (AOT-safe explicit validator, not ValidateDataAnnotations)
        services
            .AddOptions<PolarOptions>()
            .BindConfiguration("PolarSharp")
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<PolarOptions>, PolarOptionsValidator>();

        // 2. Localization (idempotent; host can register custom IPolarLocalizer before this call)
        services.AddLocalization();
        services.TryAddSingleton<IPolarLocalizer, PolarResourceLocalizer>();

        // 3. Internal infrastructure singletons
        services.AddSingleton<PolarSocketsHandlerFactory>();
        services.AddSingleton<PolarActivitySource>();
        services.AddSingleton<PolarMeter>();
        // Register as an instance so optional packages can locate and mutate it via
        // ServiceDescriptor.ImplementationInstance during service registration (before DI build).
        services.AddSingleton(new PolarInfrastructureMarker());

        // 4. Named HttpClient with full handler pipeline
        //    Pipeline order: BearerTokenHandler → IdempotencyKeyHandler → ApiVersionHandler → Resilience → SocketsHttpHandler
        services
            .AddHttpClient(HttpClientName, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<PolarOptions>>().Value;
                var baseUrl = ResolveBaseUrl(opts);
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + opts.BasePath + "/");
                client.Timeout = Timeout.InfiniteTimeSpan; // per-attempt timeout managed by resilience handler
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
                sp.GetRequiredService<PolarSocketsHandlerFactory>().Create())
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<IdempotencyKeyHandler>()
            .AddHttpMessageHandler<ApiVersionHandler>()
            .AddResilienceHandler("polar", static (builder, context) =>
            {
                var polarOpts = context.ServiceProvider
                    .GetRequiredService<IOptionsMonitor<PolarOptions>>()
                    .CurrentValue;
                var resilience = polarOpts.Resilience;

                // Per-attempt timeout — InfiniteTimeSpan is set on HttpClient itself; this enforces
                // the configured value at the Polly level so retries each get a fresh window.
                builder.AddTimeout(TimeSpan.FromMilliseconds(polarOpts.TimeoutMs));

                // Retry with exponential back-off and jitter
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = polarOpts.MaxRetries,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                });

                // Circuit breaker — scale the integer threshold (1-10) to a failure ratio (0.0-1.0)
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = resilience.CircuitBreakerFailureThreshold / 10.0,
                    MinimumThroughput = resilience.CircuitBreakerFailureThreshold,
                    SamplingDuration = TimeSpan.FromSeconds(resilience.CircuitBreakerSamplingSeconds),
                    BreakDuration = TimeSpan.FromSeconds(resilience.CircuitBreakerBreakSeconds),
                });

                // Optional hedging — only for idempotent HTTP methods (GET / HEAD)
                if (resilience.HedgeAfterMs.HasValue)
                {
                    builder.AddHedging(new HttpHedgingStrategyOptions
                    {
                        MaxHedgedAttempts = resilience.HedgeMaxAttempts,
                        Delay = TimeSpan.FromMilliseconds(resilience.HedgeAfterMs.Value),
                        ActionGenerator = static args =>
                        {
                            // Skip hedging for non-idempotent methods
                            var req = args.PrimaryContext.GetRequestMessage();
                            if (req?.Method != HttpMethod.Get && req?.Method != HttpMethod.Head)
                                return null;

                            return () => args.Callback(args.ActionContext);
                        },
                    });
                }
            });

        // Register delegating handlers as transient (required by IHttpClientFactory)
        services.AddTransient<BearerTokenHandler>();
        services.AddTransient<IdempotencyKeyHandler>();
        services.AddTransient<ApiVersionHandler>();

        // 5. PolarClient as singleton (stateless wrapper; backed by IHttpClientFactory pooling)
        services.AddSingleton<PolarClient>();

        // 6. Health check
        services
            .AddHealthChecks()
            .AddCheck<PolarHealthCheck>(
                "polar",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["polar"]);

        // 7. Startup banner + mismatch detection (logged via ILogger during app startup)
        services.AddHostedService<PolarStartupService>();

        // 8. Optional JIT warm-up — StartAsync exits immediately if WarmupOnStartup is false
        services.AddHostedService<PolarWarmupService>();

        return new PolarInfrastructureBuilder(services, configuration);
    }

    internal static string ResolveBaseUrl(PolarOptions opts)
    {
        if (opts.Mode == PolarMode.Custom && !string.IsNullOrWhiteSpace(opts.CustomBaseUrl))
            return opts.CustomBaseUrl!;

        return opts.Mode switch
        {
            PolarMode.Live => "https://api.polar.sh",
            PolarMode.Test => "https://sandbox-api.polar.sh",
            PolarMode.Custom => opts.CustomBaseUrl ?? "https://sandbox-api.polar.sh",
            _ => "https://sandbox-api.polar.sh"
        };
    }
}
