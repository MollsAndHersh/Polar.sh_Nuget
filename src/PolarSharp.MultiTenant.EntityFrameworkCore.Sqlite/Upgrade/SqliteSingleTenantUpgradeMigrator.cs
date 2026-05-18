using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Upgrade;

/// <summary>
/// SQLite implementation of <see cref="ISingleTenantUpgradeMigrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why SQLite is simpler than the other providers.</strong> The SQLite provider
/// places each tenant's data in its own <c>{tenantId}.db</c> file, with platform data
/// (the tenant registry + upgrade history) in a shared <c>master_SaaS.db</c>. There is
/// no cross-tenant table whose rows must be backfilled with a <c>TenantId</c> column —
/// per-tenant isolation is enforced at the filesystem level, not via row predicates. The
/// migrator therefore does not iterate any tenant-owned tables; its work is confined to
/// the platform-data file and the on-disk filenames.
/// </para>
/// <para>
/// <strong>Three branches at a glance.</strong>
/// <list type="number">
/// <item><description>Registry has 0 tenants — a fresh deployment that was always
/// structured for MT-readiness. Insert the supplied <c>defaultTenant</c> via the
/// registry upgrader.</description></item>
/// <item><description>Registry has 1 tenant — the existing single tenant. Treat that row
/// as authoritative; log that the supplied <c>defaultTenant</c> is being superseded so
/// operators know why the configured tenant identity did not "win".</description></item>
/// <item><description>Registry has 2+ tenants — this is already a multi-tenant
/// deployment. Mark the upgrade complete and log a warning that the migrator was invoked
/// unnecessarily.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Legacy file detection.</strong> Pre-v1.2 SQLite deployments may have left a
/// <c>data.db</c> or <c>app.db</c> file in the database directory. When such a file
/// exists and the resolved default tenant does not yet have its own
/// <c>{tenantId}.db</c>, the migrator renames the legacy file in place. The rename
/// happens via <see cref="File.Move(string, string)"/> with no copy, so even very large
/// catalogs migrate instantly.
/// </para>
/// <para>
/// <strong>What the migrator does NOT do.</strong> It never deletes anything. A surviving
/// <c>__tenants.db</c> file alongside <c>master_SaaS.db</c> after the rename triggers a
/// warning recommending the operator merge or delete it manually — the migrator refuses
/// to touch it because the two files could carry divergent rows, and reconciliation is
/// out of scope for a one-time upgrade.
/// </para>
/// </remarks>
public sealed class SqliteSingleTenantUpgradeMigrator : ISingleTenantUpgradeMigrator
{
    private static readonly IReadOnlyList<string> LegacyTenantDataFileNames = ["data.db", "app.db"];

    private static readonly JsonSerializerOptions ResultJson = new()
    {
        WriteIndented = false,
    };

    private readonly PolarTenantDbContext _db;
    private readonly ITenantRegistryUpgrader _registryUpgrader;
    private readonly SqliteMasterDatabaseLocator _locator;
    private readonly ILogger<SqliteSingleTenantUpgradeMigrator> _logger;

    /// <summary>Initializes a new <see cref="SqliteSingleTenantUpgradeMigrator"/>.</summary>
    /// <param name="db">The platform-data DbContext (opens <c>master_SaaS.db</c>).</param>
    /// <param name="registryUpgrader">The provider-agnostic tenant-registry upgrader.</param>
    /// <param name="locator">Resolved master-file location injected by the provider's <c>.UseSqlite()</c> call.</param>
    /// <param name="logger">Logger.</param>
    public SqliteSingleTenantUpgradeMigrator(
        PolarTenantDbContext db,
        ITenantRegistryUpgrader registryUpgrader,
        SqliteMasterDatabaseLocator locator,
        ILogger<SqliteSingleTenantUpgradeMigrator> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(registryUpgrader);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _registryUpgrader = registryUpgrader;
        _locator = locator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HasUpgradeCompletedAsync(CancellationToken ct)
    {
        // EnsureCreated covers the bootstrap case where the registration has not yet run
        // a migration — without it the first HasUpgradeCompletedAsync call would throw
        // "no such table: polar_upgrade_history" instead of returning false.
        await _db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);

        return await _db.UpgradeHistory
            .AsNoTracking()
            .AnyAsync(x => x.UpgradeKind == UpgradeKinds.SingleTenantToMultiTenant, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SingleTenantUpgradeResult> RunAsync(PolarTenantInfo defaultTenant, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(defaultTenant);

        var sw = Stopwatch.StartNew();
        var actionLog = new List<string>();

        if (await HasUpgradeCompletedAsync(ct).ConfigureAwait(false))
        {
            return new SingleTenantUpgradeResult
            {
                Success = true,
                AlreadyComplete = true,
                RowsStamped = 0,
                RowsStampedByEntityType = new Dictionary<string, long>(),
                Duration = sw.Elapsed,
                Message = "Upgrade marker already present in polar_upgrade_history.",
            };
        }

        var tenantCount = await _db.Tenants.AsNoTracking().CountAsync(ct).ConfigureAwait(false);

        PolarTenantInfo resolvedTenant;
        switch (tenantCount)
        {
            case 0:
                {
                    resolvedTenant = await _registryUpgrader.UpsertAsync(defaultTenant, ct).ConfigureAwait(false);
                    actionLog.Add(
                        $"Inserted default tenant '{resolvedTenant.Identifier}' " +
                        $"(Id={resolvedTenant.Id}) into the registry.");
                    break;
                }

            case 1:
                {
                    var existing = await _db.Tenants.AsNoTracking().SingleAsync(ct).ConfigureAwait(false);
                    resolvedTenant = ToTenantInfo(existing);
                    if (!string.Equals(existing.Identifier, defaultTenant.Identifier, StringComparison.Ordinal))
                    {
                        _logger.LogInformation(
                            "PolarSharp SQLite upgrade migrator: registry already contains tenant " +
                            "'{ExistingSlug}' (Id={ExistingId}); the supplied default tenant " +
                            "'{SuppliedSlug}' (Id={SuppliedId}) is being superseded by the existing entry.",
                            existing.Identifier, existing.Id,
                            defaultTenant.Identifier, defaultTenant.Id);
                        actionLog.Add(
                            $"Superseded supplied default tenant '{defaultTenant.Identifier}' " +
                            $"with existing registry entry '{existing.Identifier}'.");
                    }
                    else
                    {
                        actionLog.Add($"Reused existing registry entry '{existing.Identifier}'.");
                    }
                    break;
                }

            default:
                {
                    _logger.LogWarning(
                        "PolarSharp SQLite upgrade migrator: registry already contains {TenantCount} " +
                        "tenants — this deployment is already multi-tenant. Marking upgrade complete " +
                        "without taking further action; the migrator was invoked unnecessarily.",
                        tenantCount);
                    resolvedTenant = defaultTenant;
                    actionLog.Add($"Detected {tenantCount} existing tenants — no action taken.");
                    await StampCompletionAsync(
                        actionLog,
                        rowsStamped: 0,
                        ct).ConfigureAwait(false);
                    return new SingleTenantUpgradeResult
                    {
                        Success = true,
                        AlreadyComplete = false,
                        RowsStamped = 0,
                        RowsStampedByEntityType = new Dictionary<string, long>(),
                        Duration = sw.Elapsed,
                        Message = string.Join(" ", actionLog),
                    };
                }
        }

        TryRenameLegacyTenantDataFile(resolvedTenant, actionLog);
        WarnIfLegacyMasterFileStillPresent(actionLog);

        await StampCompletionAsync(
            actionLog,
            rowsStamped: 0,
            ct).ConfigureAwait(false);

        return new SingleTenantUpgradeResult
        {
            Success = true,
            AlreadyComplete = false,
            RowsStamped = 0,
            RowsStampedByEntityType = new Dictionary<string, long>(),
            Duration = sw.Elapsed,
            Message = string.Join(" ", actionLog),
        };
    }

    /// <summary>
    /// Inserts the completion-marker row into <c>polar_upgrade_history</c>.
    /// </summary>
    /// <param name="actionLog">Accumulated action messages — joined into the row's <c>Message</c> column.</param>
    /// <param name="rowsStamped">Total stamped row count — always zero on SQLite (per-file isolation).</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task StampCompletionAsync(
        IReadOnlyList<string> actionLog,
        long rowsStamped,
        CancellationToken ct)
    {
        var message = string.Join(" ", actionLog);
        var resultPayload = new
        {
            RowsStamped = rowsStamped,
            RowsStampedByEntityType = new Dictionary<string, long>(),
            Actions = actionLog,
        };

        _db.UpgradeHistory.Add(new UpgradeHistoryEntity
        {
            Id = Guid.NewGuid(),
            UpgradeKind = UpgradeKinds.SingleTenantToMultiTenant,
            CompletedAt = DateTimeOffset.UtcNow,
            ActorUserId = "system",
            Message = message,
            ResultSummaryJson = JsonSerializer.Serialize(resultPayload, ResultJson),
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Renames a pre-v1.2 single-tenant data file (<c>data.db</c> / <c>app.db</c>) to the
    /// per-tenant <c>{tenantId}.db</c> shape when no per-tenant file already exists.
    /// </summary>
    /// <param name="tenant">The resolved default tenant whose id seeds the new filename.</param>
    /// <param name="actionLog">Action log appended with a description of the rename, if it ran.</param>
    private void TryRenameLegacyTenantDataFile(PolarTenantInfo tenant, List<string> actionLog)
    {
        var targetPath = Path.Combine(_locator.DatabaseDirectory, $"{tenant.Id}.db");
        if (File.Exists(targetPath))
        {
            return;
        }

        foreach (var legacyName in LegacyTenantDataFileNames)
        {
            var legacyPath = Path.Combine(_locator.DatabaseDirectory, legacyName);
            if (!File.Exists(legacyPath))
            {
                continue;
            }
            File.Move(legacyPath, targetPath);
            actionLog.Add($"Renamed legacy '{legacyName}' to '{Path.GetFileName(targetPath)}'.");
            _logger.LogInformation(
                "PolarSharp SQLite upgrade migrator: renamed legacy single-tenant data file " +
                "'{LegacyPath}' to '{TargetPath}'.",
                legacyPath, targetPath);
            return;
        }
    }

    /// <summary>
    /// Logs a warning when a legacy <c>__tenants.db</c> file is still present alongside
    /// <c>master_SaaS.db</c> — the migrator refuses to auto-delete because the two files
    /// could carry divergent rows.
    /// </summary>
    /// <param name="actionLog">Action log appended with the warning summary, if it fires.</param>
    private void WarnIfLegacyMasterFileStillPresent(List<string> actionLog)
    {
        var masterFileName = Path.GetFileName(_locator.MasterDatabasePath);
        if (string.Equals(masterFileName, SqliteBuilderExtensions.LegacyTenantsFileName, StringComparison.Ordinal))
        {
            // The connection itself is open on the legacy file — the SqliteBuilderExtensions
            // fallback path already logged this; nothing more to do here.
            return;
        }

        var legacyPath = Path.Combine(_locator.DatabaseDirectory, SqliteBuilderExtensions.LegacyTenantsFileName);
        if (!File.Exists(legacyPath))
        {
            return;
        }

        _logger.LogWarning(
            "PolarSharp SQLite upgrade migrator: detected '{LegacyPath}' alongside the active " +
            "'{MasterFileName}'. The migrator does NOT auto-delete or merge the legacy file — " +
            "operator action required. Verify the legacy file is empty or obsolete, then delete " +
            "it manually.",
            legacyPath, masterFileName);
        actionLog.Add(
            $"WARN: legacy '{SqliteBuilderExtensions.LegacyTenantsFileName}' still present at " +
            $"'{_locator.DatabaseDirectory}' — operator action required.");
    }

    /// <summary>
    /// Maps a persisted <see cref="PolarTenantInfoEntity"/> to the <see cref="PolarTenantInfo"/>
    /// shape the orchestrator and registry upgrader exchange.
    /// </summary>
    private static PolarTenantInfo ToTenantInfo(PolarTenantInfoEntity entity)
        => new()
        {
            Id = entity.Id,
            Identifier = entity.Identifier,
            Name = entity.Name,
            PolarAccessToken = entity.PolarAccessToken,
            Server = entity.Server,
        };
}
