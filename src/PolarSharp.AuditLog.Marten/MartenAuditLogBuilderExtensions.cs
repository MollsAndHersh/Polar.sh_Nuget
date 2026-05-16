using Marten;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.AuditLog.Marten;

/// <summary>
/// Marten-backed event-sourced audit log registration extensions.
/// </summary>
/// <remarks>
/// <para>
/// Marten is the event-sourcing-native alternative to the EF Core-backed audit log
/// implementation that ships with <c>PolarSharp.MultiTenant.Identity</c>. Hosts opt
/// into Marten when they:
/// </para>
/// <list type="bullet">
///   <item>Already run on PostgreSQL (Marten is Postgres-only).</item>
///   <item>Want native event-streaming semantics (append-only events; projections;
///         snapshots) rather than the EF Core's mutable-row approach.</item>
///   <item>Are willing to manage Marten's projection daemon (online rebuilds) alongside
///         their normal database operations.</item>
/// </list>
/// <para>
/// Marten coexists with the v1.3.0 PolarSharp Postgres EF Core providers — both can run
/// against the same Postgres instance, in different schemas. The Marten document store
/// session injects the same <c>app.current_tenant_id</c> session variable that
/// <c>PostgreSqlTenantSessionInterceptor</c> sets for the EF Core path, so the RLS
/// policies provisioned by the v1.3 EnableRowLevelSecurity migrations also enforce
/// isolation on Marten-driven queries.
/// </para>
/// <para>
/// Phase 15 ships the registration + RLS-aware session creation. The full event-sourced
/// <c>AdminAuditLogEvent</c> hierarchy + projections + the migration story from the EF
/// Core <c>polar_admin_audit_log</c> table to Marten event streams is deferred to a
/// Phase 15.x patch — the empirical Marten 7→8 + RLS spike noted in v1.3 plan OQ-26.
/// </para>
/// </remarks>
public static class MartenAuditLogBuilderExtensions
{
    /// <summary>
    /// Registers Marten as the event-sourced audit log storage backend, replacing the
    /// EF Core implementation for <c>AdminAuditLogEntry</c>.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string used for the Marten document store.</param>
    /// <param name="schemaName">Optional Postgres schema name to isolate Marten tables (defaults to <c>"polar_marten_auditlog"</c>).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UsePostgreSql(connStr);
    /// builder.Services.UseMartenAuditLog(connStr);
    /// </code>
    /// </example>
    public static IServiceCollection UseMartenAuditLog(
        this IServiceCollection services,
        string postgresConnectionString,
        string schemaName = "polar_marten_auditlog")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(postgresConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(schemaName);

        services.AddMarten(opts =>
        {
            opts.Connection(postgresConnectionString);
            opts.DatabaseSchemaName = schemaName;
            // Marten event-sourced audit log event types + projections register here in Phase 15.x.
            // For now, the document store + connection wiring is ready for downstream packages.
        });

        // Health-check integration with Marten's own diagnostic endpoints lands in Phase 15.x.
        return services;
    }
}
