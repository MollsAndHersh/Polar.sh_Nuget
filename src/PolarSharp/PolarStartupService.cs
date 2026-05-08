using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.Versioning;

namespace PolarSharp;

/// <summary>
/// A hosted startup service that emits the mode banner, validates the API version configuration,
/// and logs the PolarSharp configuration summary before the app serves any requests.
/// </summary>
/// <remarks>
/// Runs during <c>IHost.StartAsync()</c> — before traffic is served. Completes immediately;
/// does not block the application lifecycle.
/// </remarks>
internal sealed class PolarStartupService(
    IOptions<PolarOptions> options,
    ILogger<PolarStartupService> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        EmitModeBanner(opts);
        CheckTokenPrefixMismatch(opts);
        CheckApiVersionMismatch(opts);

        if (opts.LogStartupSummary)
            LogConfigurationSummary(opts);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void EmitModeBanner(PolarOptions opts)
    {
        var baseUrl = Extensions.ServiceCollectionExtensions.ResolveBaseUrl(opts);

        if (opts.Mode == PolarMode.Live)
        {
            logger.LogWarning("════════════════════════════════════════════════════════════════");
            logger.LogWarning("PolarSharp is running in LIVE/PRODUCTION mode.");
            logger.LogWarning("API: {BaseUrl}{BasePath}", baseUrl, opts.BasePath);
            logger.LogWarning("ALL TRANSACTIONS WILL BE PROCESSED AS REAL PAYMENTS.");
            logger.LogWarning("Ensure this is intentional before serving real traffic.");
            logger.LogWarning("════════════════════════════════════════════════════════════════");
        }
        else
        {
            logger.LogInformation(
                "PolarSharp is running in TEST/SANDBOX mode. API: {BaseUrl}{BasePath}. No real transactions will be processed.",
                baseUrl, opts.BasePath);
        }
    }

    private void CheckTokenPrefixMismatch(PolarOptions opts)
    {
        var token = opts.AccessToken;
        if (string.IsNullOrEmpty(token)) return;

        var isSandboxToken = token.StartsWith("tok_sandbox_", StringComparison.Ordinal);
        var isLiveToken = token.StartsWith("tok_live_", StringComparison.Ordinal);

        if (opts.Mode == PolarMode.Live && isSandboxToken)
        {
            logger.LogWarning(
                "PolarSharp: Mode is 'Live' but AccessToken appears to be a sandbox token (prefix 'tok_sandbox_'). " +
                "Verify your configuration before processing real payments.");
        }
        else if (opts.Mode == PolarMode.Test && isLiveToken)
        {
            logger.LogWarning(
                "PolarSharp: Mode is 'Test' but AccessToken appears to be a live token (prefix 'tok_live_'). " +
                "Your live token will be used against the sandbox API — no real charges, but verify this is intentional.");
        }
    }

    private void CheckApiVersionMismatch(PolarOptions opts)
    {
        if (string.IsNullOrEmpty(opts.ApiVersion)) return;
        if (opts.ApiVersionStrictness == PolarApiVersionStrictness.Off) return;

        var configured = opts.ApiVersion;
        var bundled = PolarApiMetadata.GeneratedAgainstVersion;

        if (configured == bundled)
        {
            logger.LogInformation(
                "PolarSharp: ApiVersion '{ApiVersion}' matches the SDK's bundled version — no drift detected.",
                configured);
            return;
        }

        var isNewer = string.Compare(configured, bundled, StringComparison.Ordinal) > 0;
        var message = isNewer
            ? $"PolarSharp: Configured ApiVersion '{configured}' is newer than the bundled SDK (generated against '{bundled}'). " +
              "New endpoints or fields added after the bundled version are not exposed by this SDK. " +
              "Upgrade the PolarSharp NuGet package or pin ApiVersion to '{bundled}' to suppress this warning. " +
              "Set PolarSharp:ApiVersionStrictness=Off to disable the check entirely."
            : $"PolarSharp: Configured ApiVersion '{configured}' is older than the bundled SDK (generated against '{bundled}'). " +
              "Polar may interpret requests under an older schema; some SDK methods may target endpoints that did not exist yet. " +
              "Set PolarSharp:ApiVersionStrictness=Off to disable the check entirely.";

        logger.LogWarning("{Message}", message);
    }

    private void LogConfigurationSummary(PolarOptions opts)
    {
        var token = opts.AccessToken;
        var maskedToken = token.Length > 4
            ? $"{token[..Math.Min(token.IndexOf('_', token.IndexOf('_') + 1) + 1, token.Length)]}***{token[^4..]}"
            : "***";

        var baseUrl = Extensions.ServiceCollectionExtensions.ResolveBaseUrl(opts);
        var apiVersion = string.IsNullOrEmpty(opts.ApiVersion)
            ? $"{PolarApiMetadata.GeneratedAgainstVersion} (SDK default)"
            : opts.ApiVersion;

        logger.LogInformation(
            "PolarSharp configuration loaded:\n" +
            "  Mode        : {Mode}\n" +
            "  API         : {BaseUrl}{BasePath}\n" +
            "  ApiVersion  : {ApiVersion}\n" +
            "  AccessToken : {MaskedToken}\n" +
            "  TimeoutMs   : {TimeoutMs}\n" +
            "  MaxRetries  : {MaxRetries}\n" +
            "  Connection  : maxPerServer={MaxConn}, pooledLifetime={Lifetime}m, http2={Http2}, http3={Http3}",
            opts.Mode,
            baseUrl, opts.BasePath,
            apiVersion,
            maskedToken,
            opts.TimeoutMs,
            opts.MaxRetries,
            opts.Connection.MaxConnectionsPerServer,
            opts.Connection.PooledConnectionLifetimeMinutes,
            opts.Connection.EnableHttp2,
            opts.Connection.EnableHttp3);
    }
}
