using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.Reporting.Marten;

/// <summary>
/// Marten-backed event-sourced reporting events registration extensions.
/// </summary>
/// <remarks>
/// <para>
/// Polar's <c>/v1/events/</c> stream is inherently event-sourced — Polar emits an immutable
/// log of every business event (order.created, subscription.canceled, refund.completed, etc.).
/// Mirroring that into Marten is a natural fit: Marten's event-store primitives (append-only
/// streams + projections + snapshots) align with the source data's shape.
/// </para>
/// <para>
/// Hosts who choose this provider get:
/// </para>
/// <list type="bullet">
///   <item>Native event-streaming semantics; no impedance mismatch with Polar's data model.</item>
///   <item>Marten's projection daemon for online projection rebuilds (no maintenance window).</item>
///   <item>Snapshot support for fast point-in-time reads.</item>
/// </list>
/// <para>
/// Phase 15 ships the registration + Postgres connection. The full <c>PolarEventReplayProjector</c>
/// + <c>ReportingSnapshotEvent</c> hierarchy + projection registrations are deferred to a
/// Phase 15.x patch.
/// </para>
/// </remarks>
public static class MartenReportingBuilderExtensions
{
    /// <summary>
    /// Registers Marten as the event-sourced reporting backend, replacing the EF Core
    /// implementation for the Reporting snapshot tables.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string used for the Marten document store.</param>
    /// <param name="schemaName">Optional Postgres schema name (defaults to <c>"polar_marten_reporting"</c>).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseMartenReporting(
        this IServiceCollection services,
        string postgresConnectionString,
        string schemaName = "polar_marten_reporting")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(postgresConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(schemaName);

        services.AddMarten(opts =>
        {
            opts.Connection(postgresConnectionString);
            opts.DatabaseSchemaName = schemaName;
            // Event-type registration + projection wiring in Phase 15.x.
        });

        return services;
    }
}
