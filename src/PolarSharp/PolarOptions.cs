namespace PolarSharp;

/// <summary>
/// Configuration options for PolarSharp. Bind from the <c>PolarSharp</c> section of
/// <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Validated at startup via <see cref="PolarOptionsValidator"/> and <c>ValidateOnStart()</c>.
/// A missing <see cref="AccessToken"/>, invalid <see cref="Mode"/>, or out-of-range values
/// cause startup to fail with a descriptive error before any traffic is served.
/// </para>
/// <para>Minimal configuration:</para>
/// <code>
/// {
///   "PolarSharp": {
///     "Mode": "Test",
///     "AccessToken": "tok_sandbox_xxx"
///   }
/// }
/// </code>
/// </remarks>
public class PolarOptions
{
    /// <summary>
    /// Gets or sets the Polar.sh access token (Organization Access Token).
    /// </summary>
    /// <value>
    /// A non-empty token string. Sandbox tokens begin with <c>tok_sandbox_</c>;
    /// production tokens begin with <c>tok_live_</c>.
    /// Bound from <c>PolarSharp:AccessToken</c>.
    /// </value>
    /// <remarks>Validated as non-empty at startup. Never logged — masked in all log output.</remarks>
    public string AccessToken { get; set; } = "";

    /// <summary>
    /// Gets or sets the operating mode for this SDK instance.
    /// </summary>
    /// <value>
    /// <see cref="PolarMode.Test"/> (default) targets <c>https://sandbox-api.polar.sh/v1</c> — safe for development.
    /// <see cref="PolarMode.Live"/> targets <c>https://api.polar.sh/v1</c> — <strong>real transactions</strong>.
    /// <see cref="PolarMode.Custom"/> uses <see cref="CustomBaseUrl"/>.
    /// Bound from <c>PolarSharp:Mode</c>. Takes precedence over <see cref="Server"/> if both are set.
    /// </value>
    public PolarMode Mode { get; set; } = PolarMode.Test;

    /// <summary>
    /// Gets or sets the target Polar server. Prefer <see cref="Mode"/> for clarity.
    /// </summary>
    /// <value>
    /// Overridden by <see cref="Mode"/> when both are configured.
    /// Bound from <c>PolarSharp:Server</c>.
    /// </value>
    public PolarServer Server { get; set; } = PolarServer.Sandbox;

    /// <summary>
    /// Gets or sets a custom base URL, required when <see cref="Mode"/> is <see cref="PolarMode.Custom"/>.
    /// </summary>
    /// <value>
    /// An absolute HTTPS URI (e.g., <c>https://proxy.internal/polar</c>).
    /// Must not target RFC 1918 addresses or cloud metadata endpoints (SSRF prevention).
    /// Bound from <c>PolarSharp:CustomBaseUrl</c>.
    /// </value>
    public string? CustomBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL path version prefix appended to the base URL.
    /// </summary>
    /// <value>
    /// Must start with <c>/</c>. Default: <c>"/v1"</c>.
    /// Bound from <c>PolarSharp:BasePath</c>.
    /// </value>
    public string BasePath { get; set; } = "/v1";

    /// <summary>
    /// Gets or sets the Polar API date version to pin on every request via the <c>Polar-Version</c> header.
    /// </summary>
    /// <value>
    /// An ISO date string in <c>YYYY-MM-DD</c> format (e.g., <c>"2025-01-15"</c>).
    /// <see langword="null"/> or empty uses <see cref="Versioning.PolarApiMetadata.GeneratedAgainstVersion"/>.
    /// Bound from <c>PolarSharp:ApiVersion</c>.
    /// </value>
    /// <remarks>
    /// See <see cref="ApiVersionStrictness"/> for mismatch behaviour.
    /// Hot-reloadable via <c>IOptionsMonitor</c> — the next outbound request picks up the new value.
    /// </remarks>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Gets or sets what the SDK does when the configured <see cref="ApiVersion"/> does not match
    /// <see cref="Versioning.PolarApiMetadata.GeneratedAgainstVersion"/>.
    /// </summary>
    /// <value>
    /// <see cref="PolarApiVersionStrictness.Warn"/> (default) — logs a warning but continues.
    /// <see cref="PolarApiVersionStrictness.Strict"/> — fails startup on any mismatch.
    /// <see cref="PolarApiVersionStrictness.Off"/> — silently ignores mismatches.
    /// Bound from <c>PolarSharp:ApiVersionStrictness</c>.
    /// </value>
    public PolarApiVersionStrictness ApiVersionStrictness { get; set; } = PolarApiVersionStrictness.Warn;

    /// <summary>
    /// Gets or sets the per-attempt HTTP request timeout in milliseconds.
    /// </summary>
    /// <value>Must be between 1000 and 300000 (1s–5m). Default: 30000 (30s). Bound from <c>PolarSharp:TimeoutMs</c>.</value>
    public int TimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures.
    /// </summary>
    /// <value>Must be between 0 and 10. Default: 3. Bound from <c>PolarSharp:MaxRetries</c>.</value>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to log a startup configuration summary.
    /// </summary>
    /// <value><see langword="true"/> (default). Bound from <c>PolarSharp:LogStartupSummary</c>.</value>
    public bool LogStartupSummary { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to issue a warm-up API call after startup to JIT-compile hot paths.
    /// </summary>
    /// <value><see langword="false"/> (default — keeps startup fast). Bound from <c>PolarSharp:WarmupOnStartup</c>.</value>
    public bool WarmupOnStartup { get; set; } = false;

    /// <summary>
    /// Gets or sets the resilience (retry/circuit-breaker/hedging) sub-options.
    /// </summary>
    /// <value>Bound from <c>PolarSharp:Resilience</c>.</value>
    public PolarResilienceOptions Resilience { get; set; } = new();

    /// <summary>
    /// Gets or sets the HTTP connection pool sub-options.
    /// </summary>
    /// <value>Bound from <c>PolarSharp:Connection</c>.</value>
    public PolarConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets logging behavior sub-options.
    /// </summary>
    /// <value>Bound from <c>PolarSharp:Logging</c>.</value>
    public PolarLoggingOptions Logging { get; set; } = new();
}

/// <summary>
/// Specifies the target environment for Polar.sh API calls.
/// </summary>
public enum PolarMode
{
    /// <summary>Targets the Polar sandbox (<c>https://sandbox-api.polar.sh/v1</c>). No real transactions.</summary>
    Test,

    /// <summary>Targets Polar production (<c>https://api.polar.sh/v1</c>). <strong>Real transactions.</strong></summary>
    Live,

    /// <summary>Targets a custom base URL specified in <see cref="PolarOptions.CustomBaseUrl"/>.</summary>
    Custom
}

/// <summary>
/// Specifies the Polar server endpoint. Prefer <see cref="PolarMode"/> for clarity.
/// </summary>
public enum PolarServer
{
    /// <summary>Polar production environment.</summary>
    Production,

    /// <summary>Polar sandbox environment.</summary>
    Sandbox,

    /// <summary>A custom host defined by <see cref="PolarOptions.CustomBaseUrl"/>.</summary>
    Custom
}

/// <summary>
/// Controls the SDK's behaviour when the configured <c>ApiVersion</c> mismatches
/// the SDK's bundled generated version.
/// </summary>
public enum PolarApiVersionStrictness
{
    /// <summary>Log a warning but allow the app to start and serve traffic.</summary>
    Warn,

    /// <summary>Fail startup immediately with a clear error message listing both versions.</summary>
    Strict,

    /// <summary>Silently ignore any version mismatch — no logging, no error.</summary>
    Off
}

/// <summary>
/// Resilience sub-options for retry, circuit-breaker, and hedging behaviour.
/// </summary>
public class PolarResilienceOptions
{
    /// <summary>
    /// Gets or sets the number of failures within <see cref="CircuitBreakerSamplingSeconds"/> that trips the circuit breaker.
    /// </summary>
    /// <value>Default: 5. Bound from <c>PolarSharp:Resilience:CircuitBreakerFailureThreshold</c>.</value>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the sliding window (in seconds) for circuit-breaker failure counting.
    /// </summary>
    /// <value>Default: 30. Bound from <c>PolarSharp:Resilience:CircuitBreakerSamplingSeconds</c>.</value>
    public int CircuitBreakerSamplingSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long (in seconds) the circuit stays open before a half-open probe.
    /// </summary>
    /// <value>Default: 15. Bound from <c>PolarSharp:Resilience:CircuitBreakerBreakSeconds</c>.</value>
    public int CircuitBreakerBreakSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the delay in milliseconds after which a hedged duplicate request is sent
    /// for idempotent (<c>GET</c>/<c>HEAD</c>) operations. <see langword="null"/> disables hedging.
    /// </summary>
    /// <value>
    /// <see langword="null"/> (default — hedging disabled).
    /// Set to e.g. 250 to hedge after 250 ms; whichever response arrives first is used.
    /// Bound from <c>PolarSharp:Resilience:HedgeAfterMs</c>.
    /// </value>
    public int? HedgeAfterMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of parallel hedged attempts (including the original request).
    /// </summary>
    /// <value>Default: 2. Only meaningful when <see cref="HedgeAfterMs"/> is set. Bound from <c>PolarSharp:Resilience:HedgeMaxAttempts</c>.</value>
    public int HedgeMaxAttempts { get; set; } = 2;
}

/// <summary>
/// HTTP connection pool sub-options for <see cref="PolarOptions"/>.
/// </summary>
public class PolarConnectionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of simultaneous connections per Polar host.
    /// </summary>
    /// <value>Must be between 1 and 1000. Default: 100. Bound from <c>PolarSharp:Connection:MaxConnectionsPerServer</c>.</value>
    public int MaxConnectionsPerServer { get; set; } = 100;

    /// <summary>
    /// Gets or sets how long (in minutes) a pooled connection lives before being recycled for DNS rotation.
    /// </summary>
    /// <value>Must be between 1 and 1440. Default: 15. Bound from <c>PolarSharp:Connection:PooledConnectionLifetimeMinutes</c>.</value>
    public int PooledConnectionLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets how long (in minutes) an idle pooled connection is kept before being closed.
    /// </summary>
    /// <value>Must be between 1 and 60. Default: 2. Bound from <c>PolarSharp:Connection:PooledConnectionIdleTimeoutMinutes</c>.</value>
    public int PooledConnectionIdleTimeoutMinutes { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether HTTP/2 is enabled for connections to Polar.
    /// </summary>
    /// <value><see langword="true"/> (default). Bound from <c>PolarSharp:Connection:EnableHttp2</c>.</value>
    public bool EnableHttp2 { get; set; } = true;

    /// <summary>
    /// Gets or sets whether HTTP/3 (QUIC) is enabled for connections to Polar. Experimental in .NET 10.
    /// </summary>
    /// <value><see langword="false"/> (default — opt-in only). Bound from <c>PolarSharp:Connection:EnableHttp3</c>.</value>
    public bool EnableHttp3 { get; set; } = false;

    /// <summary>
    /// Gets or sets whether multiple HTTP/2 connections may be established to the same host for parallelism.
    /// </summary>
    /// <value><see langword="true"/> (default). Bound from <c>PolarSharp:Connection:EnableMultipleHttp2Connections</c>.</value>
    public bool EnableMultipleHttp2Connections { get; set; } = true;
}

/// <summary>
/// Logging behavior sub-options for <see cref="PolarOptions"/>.
/// </summary>
public class PolarLoggingOptions
{
    /// <summary>
    /// Gets or sets whether PII (email addresses, customer names, error detail) is masked in log output.
    /// </summary>
    /// <value>
    /// <see langword="true"/> (default) — GDPR-compliant production default.
    /// Set to <see langword="false"/> in development environments for full debug output.
    /// Bound from <c>PolarSharp:Logging:RedactPii</c>.
    /// </value>
    public bool RedactPii { get; set; } = true;

    /// <summary>
    /// Gets or sets whether resource IDs (order ID, customer ID, etc.) are hashed before logging.
    /// </summary>
    /// <value>
    /// <see langword="false"/> (default — IDs are not PII under GDPR in most interpretations).
    /// Bound from <c>PolarSharp:Logging:RedactIdsToHashes</c>.
    /// </value>
    public bool RedactIdsToHashes { get; set; } = false;
}
