using System.Diagnostics;
using System.Reflection;

namespace PolarSharp.Telemetry;

/// <summary>
/// Provides the <see cref="ActivitySource"/> used by PolarSharp to emit distributed tracing spans
/// for every outbound Polar API call.
/// </summary>
/// <remarks>
/// Integrates with OpenTelemetry via the standard <see cref="ActivitySource"/> listener mechanism.
/// Zero overhead when no listener is attached (AOT-safe, allocation-free when unused).
/// <para>
/// Each API call span is tagged with:
/// <list type="bullet">
///   <item><c>polar.resource</c> — resource area (e.g., <c>"orders"</c>).</item>
///   <item><c>polar.operation</c> — operation name (e.g., <c>"get"</c>, <c>"list"</c>).</item>
///   <item><c>http.status_code</c> — HTTP response status code.</item>
///   <item><c>polar.request_id</c> — Polar's <c>x-request-id</c> response header value.</item>
///   <item><c>error.type</c> — set on failure; the <see cref="PolarError"/> discriminator.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class PolarActivitySource : IDisposable
{
    /// <summary>Gets the name of the <see cref="ActivitySource"/>.</summary>
    public const string SourceName = "PolarSharp";

    private static readonly string Version =
        typeof(PolarActivitySource).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

    /// <summary>Gets the underlying <see cref="ActivitySource"/>.</summary>
    public ActivitySource Source { get; } = new(SourceName, Version);

    /// <summary>
    /// Starts a new tracing span for a Polar API call.
    /// </summary>
    /// <param name="resource">The Polar resource area (e.g., <c>"orders"</c>).</param>
    /// <param name="operation">The operation name (e.g., <c>"get"</c>).</param>
    /// <param name="tenantId">The tenant identifier (empty string for single-tenant).</param>
    /// <returns>
    /// An <see cref="Activity"/> span, or <see langword="null"/> when no listener is attached.
    /// Always use <c>using var _ = StartActivity(...)</c> — null Activity is safe to dispose.
    /// </returns>
    public Activity? StartActivity(string resource, string operation, string tenantId = "")
    {
        var activity = Source.StartActivity($"polar.{resource}.{operation}", ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag("polar.resource", resource);
        activity.SetTag("polar.operation", operation);
        if (!string.IsNullOrEmpty(tenantId))
            activity.SetTag("polar.tenant_id", tenantId);

        return activity;
    }

    /// <inheritdoc/>
    public void Dispose() => Source.Dispose();
}
