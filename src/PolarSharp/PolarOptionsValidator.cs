using Microsoft.Extensions.Options;
using PolarSharp.Versioning;
using System.Text.RegularExpressions;

namespace PolarSharp;

/// <summary>
/// Validates <see cref="PolarOptions"/> at startup using <see cref="IValidateOptions{TOptions}"/>.
/// Zero-reflection implementation — fully AOT-safe.
/// </summary>
/// <remarks>
/// <c>ValidateDataAnnotations()</c> reads attributes via reflection at runtime and is NOT AOT-safe.
/// This explicit implementation performs identical validation without any reflection.
/// <para>
/// Registered with <c>services.AddOptions&lt;PolarOptions&gt;().ValidateOnStart()</c> so that
/// misconfiguration causes a clear startup exception before any request is served.
/// </para>
/// </remarks>
internal sealed partial class PolarOptionsValidator : IValidateOptions<PolarOptions>
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled)]
    private static partial Regex IsoDatePattern();

    private static readonly HashSet<string> BlockedHostPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "169.254.", "127.", "10.", "192.168.",
        "172.16.", "172.17.", "172.18.", "172.19.", "172.20.",
        "172.21.", "172.22.", "172.23.", "172.24.", "172.25.",
        "172.26.", "172.27.", "172.28.", "172.29.", "172.30.", "172.31.",
        "::1"
    };

    /// <summary>
    /// Validates all <see cref="PolarOptions"/> properties and returns a list of failures.
    /// </summary>
    /// <param name="name">The options name (ignored — there is only one instance).</param>
    /// <param name="options">The options to validate.</param>
    /// <returns>
    /// <see cref="ValidateOptionsResult.Success"/> if all validations pass;
    /// otherwise <see cref="ValidateOptionsResult.Fail(IEnumerable{string})"/> with all failures listed.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, PolarOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        ValidateAccessToken(options, failures);
        ValidateMode(options, failures);
        ValidateCustomBaseUrl(options, failures);
        ValidateBasePath(options, failures);
        ValidateApiVersion(options, failures);
        ValidateTimeout(options, failures);
        ValidateRetries(options, failures);
        ValidateConnection(options, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateAccessToken(PolarOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.AccessToken))
            failures.Add("PolarSharp:AccessToken is required and must not be empty.");
    }

    private static void ValidateMode(PolarOptions options, List<string> failures)
    {
        if (!Enum.IsDefined(options.Mode))
            failures.Add($"PolarSharp:Mode '{options.Mode}' is not a valid value. Must be 'Test', 'Live', or 'Custom'.");
    }

    private static void ValidateCustomBaseUrl(PolarOptions options, List<string> failures)
    {
        if (options.Mode != PolarMode.Custom) return;

        if (string.IsNullOrWhiteSpace(options.CustomBaseUrl))
        {
            failures.Add("PolarSharp:CustomBaseUrl is required when Mode is 'Custom'.");
            return;
        }

        if (!Uri.TryCreate(options.CustomBaseUrl, UriKind.Absolute, out var uri))
        {
            failures.Add("PolarSharp:CustomBaseUrl must be an absolute URI.");
            return;
        }

        if (uri.Scheme != "https")
        {
            failures.Add("PolarSharp:CustomBaseUrl must use the HTTPS scheme.");
            return;
        }

        // SSRF prevention: block RFC 1918, loopback, and cloud metadata endpoints
        var host = uri.Host;
        foreach (var blocked in BlockedHostPrefixes)
        {
            if (host.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"PolarSharp:CustomBaseUrl targets a blocked host address ({host}). " +
                             "Internal addresses, loopback, and cloud metadata endpoints are not allowed.");
                return;
            }
        }

        // Block IPv6 ULA (fc00::/7) — covers fc00:: through fdff:: range.
        // URI host for IPv6 literals is either "[fc00::1]" (with brackets) or "fc00::1" (without).
        // Check the normalized host for the fc/fd prefix.
        var normalizedHost = host.Trim('[', ']');
        if (normalizedHost.StartsWith("fc", StringComparison.OrdinalIgnoreCase) ||
            normalizedHost.StartsWith("fd", StringComparison.OrdinalIgnoreCase))
        {
            // Only block if it looks like an IPv6 address (contains colons).
            if (normalizedHost.Contains(':', StringComparison.Ordinal))
            {
                failures.Add($"PolarSharp:CustomBaseUrl targets a blocked host address ({host}). " +
                             "Internal addresses, loopback, and cloud metadata endpoints are not allowed.");
                return;
            }
        }
    }

    private static void ValidateBasePath(PolarOptions options, List<string> failures)
    {
        if (!string.IsNullOrEmpty(options.BasePath) && !options.BasePath.StartsWith('/'))
            failures.Add("PolarSharp:BasePath must start with '/' (e.g., '/v1').");
    }

    private static void ValidateApiVersion(PolarOptions options, List<string> failures)
    {
        if (string.IsNullOrEmpty(options.ApiVersion)) return;

        if (!IsoDatePattern().IsMatch(options.ApiVersion))
        {
            failures.Add($"PolarSharp:ApiVersion '{options.ApiVersion}' must be an ISO date in 'YYYY-MM-DD' format " +
                         "(e.g., '2025-01-15'), or null to use the SDK's bundled version.");
            return;
        }

        if (options.ApiVersionStrictness == PolarApiVersionStrictness.Strict
            && options.ApiVersion != PolarApiMetadata.GeneratedAgainstVersion)
        {
            failures.Add($"PolarSharp:ApiVersion '{options.ApiVersion}' does not match the SDK's bundled version " +
                         $"'{PolarApiMetadata.GeneratedAgainstVersion}'. Strict mode requires an exact match. " +
                         "Pin to the SDK version or set ApiVersionStrictness to 'Warn'.");
        }
    }

    private static void ValidateTimeout(PolarOptions options, List<string> failures)
    {
        if (options.TimeoutMs is < 1_000 or > 300_000)
            failures.Add($"PolarSharp:TimeoutMs ({options.TimeoutMs}) must be between 1000 (1s) and 300000 (5m).");
    }

    private static void ValidateRetries(PolarOptions options, List<string> failures)
    {
        if (options.MaxRetries is < 0 or > 10)
            failures.Add($"PolarSharp:MaxRetries ({options.MaxRetries}) must be between 0 and 10.");
    }

    private static void ValidateConnection(PolarOptions options, List<string> failures)
    {
        var conn = options.Connection;

        if (conn.MaxConnectionsPerServer is < 1 or > 1_000)
            failures.Add($"PolarSharp:Connection:MaxConnectionsPerServer ({conn.MaxConnectionsPerServer}) must be between 1 and 1000.");

        if (conn.PooledConnectionLifetimeMinutes is < 1 or > 1_440)
            failures.Add($"PolarSharp:Connection:PooledConnectionLifetimeMinutes ({conn.PooledConnectionLifetimeMinutes}) must be between 1 and 1440.");

        if (conn.PooledConnectionIdleTimeoutMinutes is < 1 or > 60)
            failures.Add($"PolarSharp:Connection:PooledConnectionIdleTimeoutMinutes ({conn.PooledConnectionIdleTimeoutMinutes}) must be between 1 and 60.");
    }
}
