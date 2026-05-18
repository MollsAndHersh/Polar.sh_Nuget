namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// EF Core-mapped row recording the completion of a one-time upgrade procedure (currently
/// the single-tenant -> multi-tenant data backfill, but the table is sized to accommodate
/// future upgrade kinds without a schema migration).
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="ISingleTenantUpgradeMigrator"/> writes exactly one row here per
/// successful upgrade run. The presence of a row with a given <see cref="UpgradeKind"/>
/// is the completion marker the orchestrator reads via
/// <see cref="ISingleTenantUpgradeMigrator.HasUpgradeCompletedAsync(CancellationToken)"/>
/// to short-circuit subsequent boots.
/// </para>
/// <para>
/// <strong>Schema placement.</strong> The entity lives in the provider-agnostic base
/// package because the shape is identical across all five EF Core providers (SqlServer,
/// SQLite, PostgreSQL, MariaDB, Cosmos). Each provider package supplies its own EF
/// migration that creates the backing table — the per-provider migration is the one
/// place the CREATE TABLE DDL diverges (column types and the SQLite-specific JSON1
/// extension affect column declarations).
/// </para>
/// <para>
/// <strong>Audit posture.</strong> The <see cref="ActorUserId"/> column captures who
/// triggered the upgrade — <c>"system"</c> when the hosted service ran it on startup,
/// or an operator user id when invoked via the CLI. <see cref="ResultSummaryJson"/>
/// stores the full <see cref="SingleTenantUpgradeResult"/> serialized for forensic
/// audit; the column is unbounded so per-entity-type row counts are preserved.
/// </para>
/// </remarks>
public sealed class UpgradeHistoryEntity
{
    /// <summary>Gets or sets the row identifier.</summary>
    /// <value>A fresh <see cref="System.Guid"/> per row; never reused across upgrades.</value>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the kind of upgrade this row records.</summary>
    /// <value>
    /// Free-form string identifying the upgrade procedure (e.g.
    /// <c>"SingleTenantToMultiTenant"</c>). See
    /// <see cref="UpgradeKinds"/> for the well-known constants used by the framework.
    /// </value>
    public string UpgradeKind { get; set; } = string.Empty;

    /// <summary>Gets or sets the wall-clock instant the upgrade completed.</summary>
    /// <value>UTC <see cref="DateTimeOffset"/> captured by the migrator immediately before insert.</value>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>Gets or sets the actor that triggered the upgrade.</summary>
    /// <value>
    /// <c>"system"</c> when the orchestrator's hosted service ran the upgrade during host
    /// startup, or an operator user identifier when invoked out-of-band via the CLI.
    /// <see langword="null"/> only when the schema row was inserted by an out-of-band tool
    /// that did not know the actor.
    /// </value>
    public string? ActorUserId { get; set; }

    /// <summary>Gets or sets the human-readable summary message recorded by the migrator.</summary>
    /// <value>
    /// Plain-text description of what the migrator did or why it skipped (e.g.
    /// <c>"Created default tenant 'default' and renamed legacy data.db -> {tenantId}.db"</c>).
    /// </value>
    public string? Message { get; set; }

    /// <summary>Gets or sets the full <see cref="SingleTenantUpgradeResult"/> serialized as JSON.</summary>
    /// <value>
    /// The JSON serialization of the result record produced by
    /// <see cref="ISingleTenantUpgradeMigrator.RunAsync(PolarSharp.MultiTenant.PolarTenantInfo, CancellationToken)"/>.
    /// Stored verbatim so forensic audit retains the full per-entity-type row-count breakdown,
    /// even if the in-memory result record grows new fields in a future release.
    /// </value>
    public string? ResultSummaryJson { get; set; }
}

/// <summary>
/// Well-known constants for <see cref="UpgradeHistoryEntity.UpgradeKind"/>.
/// </summary>
public static class UpgradeKinds
{
    /// <summary>The single-tenant -> multi-tenant data backfill upgrade.</summary>
    public const string SingleTenantToMultiTenant = "SingleTenantToMultiTenant";
}
