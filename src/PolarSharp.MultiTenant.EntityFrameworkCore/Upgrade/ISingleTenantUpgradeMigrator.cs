using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// Per-provider contract for migrating a single-tenant deployment's data into the
/// multi-tenant shape. Each EF Core provider package (SQL Server, SQLite, PostgreSQL,
/// MariaDB, Cosmos) supplies its own implementation registered into DI.
/// </summary>
/// <remarks>
/// <para>
/// The contract: assign a default tenant to all existing single-tenant-mode data; register
/// that tenant in the tenant store; mark the upgrade complete so the migrator does not run
/// again on subsequent startups. Implementations MUST be idempotent — calling
/// <see cref="RunAsync(PolarTenantInfo, CancellationToken)"/> twice produces the same
/// observable result as calling it once.
/// </para>
/// <para>
/// <strong>How "complete" is tracked</strong> is provider-specific: relational providers
/// typically write a row to a <c>__polar_mt_upgrade_history</c> table; document providers
/// stamp a marker document. <see cref="HasUpgradeCompletedAsync(CancellationToken)"/> reads
/// the same marker so the orchestrator can short-circuit on subsequent boots without
/// scanning every table.
/// </para>
/// <para>
/// <strong>Concurrency posture.</strong> The upgrade is not designed to run on multiple
/// hosts simultaneously. The orchestrator runs it during host startup before traffic is
/// accepted; deployers using rolling deploys should drain to one instance before flipping
/// the multi-tenant switch.
/// </para>
/// </remarks>
public interface ISingleTenantUpgradeMigrator
{
    /// <summary>
    /// Indicates whether this provider's upgrade has already completed on this database.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the migrator's completion marker is present in the
    /// underlying store; <see langword="false"/> otherwise.
    /// </returns>
    Task<bool> HasUpgradeCompletedAsync(CancellationToken ct);

    /// <summary>
    /// Runs the single-tenant -> multi-tenant data upgrade. Idempotent — safe to invoke
    /// repeatedly. When the upgrade has already completed, returns a
    /// <see cref="SingleTenantUpgradeResult"/> with <see cref="SingleTenantUpgradeResult.AlreadyComplete"/>
    /// set to <see langword="true"/> and zero rows stamped.
    /// </summary>
    /// <param name="defaultTenant">
    /// The tenant info the migrator assigns existing single-tenant rows to. Must carry a
    /// well-formed <see cref="PolarTenantInfo.Id"/> (GUID string) and
    /// <see cref="PolarTenantInfo.Identifier"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A structured outcome describing what the migrator did.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="defaultTenant"/> is <see langword="null"/>.</exception>
    Task<SingleTenantUpgradeResult> RunAsync(PolarTenantInfo defaultTenant, CancellationToken ct);
}

/// <summary>
/// Outcome of a single-tenant -> multi-tenant upgrade attempt.
/// </summary>
/// <remarks>
/// Returned by every <see cref="ISingleTenantUpgradeMigrator.RunAsync(PolarTenantInfo, CancellationToken)"/>
/// call. The orchestrator logs the result and surfaces it to operators via the CLI
/// (<c>dotnet polar-mt upgrade</c>) for audit purposes.
/// </remarks>
public sealed record SingleTenantUpgradeResult
{
    /// <summary>
    /// Gets a value indicating whether the migrator successfully completed (or was already complete).
    /// </summary>
    /// <value><see langword="true"/> on success or when <see cref="AlreadyComplete"/> is true; <see langword="false"/> on any failure.</value>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets a value indicating whether the migrator detected the upgrade had already run and skipped.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when the completion marker was already present at the start of the run;
    /// <see langword="false"/> when the migrator actually performed work.
    /// </value>
    public required bool AlreadyComplete { get; init; }

    /// <summary>
    /// Gets the total number of rows the migrator stamped with the default tenant id across
    /// all entity types. Zero when <see cref="AlreadyComplete"/> is <see langword="true"/>.
    /// </summary>
    public required long RowsStamped { get; init; }

    /// <summary>
    /// Gets the per-entity-type breakdown of stamped row counts. Useful for audit logs and
    /// operator-facing reporting.
    /// </summary>
    /// <value>
    /// A map of entity-type CLR name (e.g. <c>"PolarProductCacheEntity"</c>) to the row count
    /// stamped on that entity. Empty when <see cref="AlreadyComplete"/> is <see langword="true"/>.
    /// </value>
    public required IReadOnlyDictionary<string, long> RowsStampedByEntityType { get; init; }

    /// <summary>
    /// Gets the wall-clock duration of the upgrade attempt, measured from the start of
    /// <see cref="ISingleTenantUpgradeMigrator.RunAsync(PolarTenantInfo, CancellationToken)"/>
    /// until it returned.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets an optional human-readable message describing the outcome — typically used to
    /// explain why the upgrade was skipped or to surface a non-fatal warning.
    /// </summary>
    public string? Message { get; init; }
}
