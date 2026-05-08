using System.Security.Cryptography;
using System.Text;

namespace PolarSharp.Telemetry;

/// <summary>
/// Redacts personally identifiable information (PII) from Polar log scope dictionaries
/// before entries leave the process to any log sink.
/// </summary>
/// <remarks>
/// <para>
/// Activated when <see cref="PolarLoggingOptions.RedactPii"/> is <see langword="true"/>
/// (the default in production). Customer emails are masked, names are shortened to initials,
/// and error detail strings are truncated. Order IDs, product IDs, and resource identifiers
/// are retained verbatim because they are operational, not personal, data.
/// </para>
/// <para>
/// IP addresses are always SHA-256-hashed (first 8 hex chars) regardless of the
/// <see cref="PolarLoggingOptions.RedactPii"/> setting, because even a hashed IP remains
/// correlatable across log entries while preventing the raw address from being stored.
/// </para>
/// <para>GDPR compliance note: only the bare minimum needed for incident correlation
/// is retained after redaction.</para>
/// </remarks>
internal static class PolarPiiRedactor
{
    private const int ErrorDetailMaxLength = 200;

    private static readonly HashSet<string> MaskedEmailKeys =
    [
        "polar.customer_email",
    ];

    private static readonly HashSet<string> InitialsKeys =
    [
        "polar.customer_name",
    ];

    private static readonly HashSet<string> TruncatedKeys =
    [
        "polar.error_detail",
    ];

    private static readonly HashSet<string> IpHashKeys =
    [
        "polar.source_ip",
        "polar.remote_ip",
    ];

    /// <summary>
    /// Returns a copy of <paramref name="scope"/> with PII fields redacted or hashed.
    /// Fields not listed in any redaction set are passed through unchanged.
    /// </summary>
    /// <param name="scope">The log scope dictionary to redact.</param>
    /// <returns>A new dictionary with the same keys but PII values replaced.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is <see langword="null"/>.</exception>
    public static IReadOnlyDictionary<string, object?> Redact(IReadOnlyDictionary<string, object?> scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var result = new Dictionary<string, object?>(scope.Count);

        foreach (var (key, value) in scope)
        {
            result[key] = MaskedEmailKeys.Contains(key)  ? MaskEmail(value)
                        : InitialsKeys.Contains(key)     ? ToInitials(value)
                        : TruncatedKeys.Contains(key)    ? Truncate(value)
                        : IpHashKeys.Contains(key)       ? HashIp(value)
                        : value;
        }

        return result;
    }

    /// <summary>
    /// Hashes a source IP address string for privacy-preserving logging.
    /// The raw IP is never stored; the hash prefix enables cross-entry correlation.
    /// </summary>
    /// <param name="ipAddress">The raw IP address string, or <see langword="null"/>.</param>
    /// <returns>
    /// A string of the form <c>"sha256:{firstEightHexChars}"</c>, or <c>null</c> if
    /// <paramref name="ipAddress"/> is <see langword="null"/> or empty.
    /// </returns>
    public static string? HashIp(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return $"sha256:{Convert.ToHexString(bytes)[..8].ToLowerInvariant()}";
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static object? MaskEmail(object? value)
    {
        if (value is not string email || string.IsNullOrWhiteSpace(email))
            return value;

        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0)
            return "***";

        return $"{email[0]}***{email[at..]}";
    }

    private static object? ToInitials(object? value)
    {
        if (value is not string name || string.IsNullOrWhiteSpace(name))
            return value;

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(p => p[0])) + ".";
    }

    private static object? Truncate(object? value)
    {
        if (value is not string s)
            return value;

        return s.Length <= ErrorDetailMaxLength
            ? s
            : string.Concat(s.AsSpan(0, ErrorDetailMaxLength), "…");
    }

    private static object? HashIp(object? value)
        => value is string s ? HashIp(s) : value;
}
