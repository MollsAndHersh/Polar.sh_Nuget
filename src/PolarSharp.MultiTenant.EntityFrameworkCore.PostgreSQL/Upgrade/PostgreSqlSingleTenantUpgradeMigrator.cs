using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.Upgrade;

/// <summary>
/// PostgreSQL implementation of <see cref="ISingleTenantUpgradeMigrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why PostgreSQL differs from SQLite.</strong> The PostgreSQL provider uses a
/// single shared database with rows discriminated by a <c>TenantId</c> column on every
/// <see cref="ITenantOwned"/> entity. The migrator therefore must <em>backfill</em>
/// <c>TenantId</c> on every existing row of every tenant-owned table, not merely create
/// the tenant registry entry. Bulk <c>UPDATE</c> statements run via
/// <see cref="RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade, string, object[])"/>
/// so very large tables migrate without loading rows into memory.
/// </para>
/// <para>
/// <strong>RLS interaction.</strong> The shipped <c>EnableRowLevelSecurity</c> migration
/// issues <c>ALTER TABLE … ENABLE ROW LEVEL SECURITY</c> + <c>FORCE ROW LEVEL SECURITY</c>
/// and creates a <c>tenant_isolation</c> policy that filters by the
/// <c>app.current_tenant_id</c> session variable. The migrator works around the policy by
/// setting the <c>app.is_app_master_admin</c> session variable to <c>true</c> for the
/// duration of the backfill (the same bypass used by <c>[AllowCrossTenant]</c> routes).
/// Both variables are set with <c>SET LOCAL</c> so the bypass scope is bounded by the
/// surrounding transaction — once the transaction commits or rolls back the variable
/// is gone, even if the underlying connection is checked back into the pool.
/// </para>
/// <para>
/// <strong>Transactional posture.</strong> The whole backfill runs in a single transaction
/// so a mid-flight failure rolls back cleanly. The completion-marker insert into
/// <c>polar_upgrade_history</c> is part of the same transaction — operators never see a
/// half-stamped database with no completion record.
/// </para>
/// </remarks>
public sealed class PostgreSqlSingleTenantUpgradeMigrator : ISingleTenantUpgradeMigrator
{
    private static readonly JsonSerializerOptions ResultJson = new()
    {
        WriteIndented = false,
    };

    private readonly PolarTenantDbContext _db;
    private readonly ITenantRegistryUpgrader _registryUpgrader;
    private readonly ILogger<PostgreSqlSingleTenantUpgradeMigrator> _logger;

    /// <summary>Initializes a new <see cref="PostgreSqlSingleTenantUpgradeMigrator"/>.</summary>
    /// <param name="db">The tenant-registry DbContext (also hosts the upgrade-history table).</param>
    /// <param name="registryUpgrader">The provider-agnostic tenant-registry upgrader.</param>
    /// <param name="logger">Logger.</param>
    public PostgreSqlSingleTenantUpgradeMigrator(
        PolarTenantDbContext db,
        ITenantRegistryUpgrader registryUpgrader,
        ILogger<PostgreSqlSingleTenantUpgradeMigrator> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(registryUpgrader);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _registryUpgrader = registryUpgrader;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HasUpgradeCompletedAsync(CancellationToken ct)
    {
        // EnsureCreated covers the bootstrap case where the registration has not yet run
        // a migration — without it the first HasUpgradeCompletedAsync call would throw
        // "relation polar_upgrade_history does not exist" instead of returning false.
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

        var resolvedTenant = await _registryUpgrader.UpsertAsync(defaultTenant, ct).ConfigureAwait(false);
        actionLog.Add(
            $"Resolved default tenant '{resolvedTenant.Identifier}' (Id={resolvedTenant.Id}) in registry.");

        var perEntityCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        long totalStamped = 0;

        await using var tx = await _db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            // SET LOCAL is transaction-scoped — the bypass disappears when the transaction
            // commits or rolls back. No try/finally needed to reset.
            await SetTransactionRlsBypassAsync(_db, ct).ConfigureAwait(false);

            foreach (var entityType in EnumerateTenantOwnedEntityTypes(_db))
            {
                var stamped = await BackfillEntityTenantIdAsync(
                    _db, entityType, resolvedTenant.Id!, ct).ConfigureAwait(false);
                perEntityCounts[entityType.ClrType.Name] = stamped;
                totalStamped += stamped;
                if (stamped > 0)
                {
                    actionLog.Add($"Stamped {stamped} row(s) of {entityType.ClrType.Name}.");
                }
            }

            await InsertCompletionRowAsync(actionLog, totalStamped, perEntityCounts, ct).ConfigureAwait(false);

            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation(
            "PolarSharp PostgreSQL upgrade migrator: stamped {RowCount} row(s) across {EntityCount} entity type(s) " +
            "to tenant '{Tenant}' (Id={TenantId}).",
            totalStamped, perEntityCounts.Count, resolvedTenant.Identifier, resolvedTenant.Id);

        return new SingleTenantUpgradeResult
        {
            Success = true,
            AlreadyComplete = false,
            RowsStamped = totalStamped,
            RowsStampedByEntityType = perEntityCounts,
            Duration = sw.Elapsed,
            Message = string.Join(" ", actionLog),
        };
    }

    /// <summary>
    /// Sets the <c>app.is_app_master_admin</c> session variable to <c>true</c> for the
    /// scope of the current transaction so the shipped RLS policy treats the migrator as
    /// an AppMasterAdmin bypass.
    /// </summary>
    /// <param name="db">The DbContext whose connection is used for the call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Uses <c>SET LOCAL</c> rather than <c>set_config(..., false)</c> deliberately: <c>SET
    /// LOCAL</c> is bounded by the surrounding transaction. The variable disappears on
    /// commit or rollback so a crashed migrator cannot leak the bypass back into the
    /// connection pool.
    /// </remarks>
    private static async Task SetTransactionRlsBypassAsync(PolarTenantDbContext db, CancellationToken ct)
    {
        // SET LOCAL does not accept parameter placeholders for the value — but the literal
        // here is a constant string, not user input, so there is no injection surface.
        await db.Database
            .ExecuteSqlRawAsync("SET LOCAL app.is_app_master_admin = 'true';", ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Enumerates every entity type in the model that implements <see cref="ITenantOwned"/>.
    /// </summary>
    /// <param name="db">The DbContext to walk.</param>
    /// <returns>The matching <see cref="IEntityType"/> instances.</returns>
    private static IEnumerable<IEntityType> EnumerateTenantOwnedEntityTypes(PolarTenantDbContext db)
        => db.Model.GetEntityTypes()
            .Where(et => typeof(ITenantOwned).IsAssignableFrom(et.ClrType));

    /// <summary>
    /// Issues a single bulk <c>UPDATE</c> against the supplied entity's table that sets
    /// <c>TenantId</c> to <paramref name="tenantId"/> wherever the column is currently NULL
    /// or empty.
    /// </summary>
    /// <param name="db">The DbContext.</param>
    /// <param name="entityType">The EF entity type to back-fill.</param>
    /// <param name="tenantId">The tenant id to stamp.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of rows that the update modified.</returns>
    private static async Task<long> BackfillEntityTenantIdAsync(
        PolarTenantDbContext db,
        IEntityType entityType,
        string tenantId,
        CancellationToken ct)
    {
        var tableName = entityType.GetTableName();
        if (string.IsNullOrEmpty(tableName))
        {
            return 0;
        }
        var schema = entityType.GetSchema();
        var qualifiedTable = string.IsNullOrEmpty(schema)
            ? $"\"{tableName}\""
            : $"\"{schema}\".\"{tableName}\"";

        var sql =
            $"UPDATE {qualifiedTable} SET \"TenantId\" = {{0}} " +
            "WHERE \"TenantId\" IS NULL OR \"TenantId\" = '';";
        var rows = await db.Database.ExecuteSqlRawAsync(sql, [tenantId], ct).ConfigureAwait(false);
        return rows;
    }

    /// <summary>
    /// Inserts the completion-marker row into <c>polar_upgrade_history</c>.
    /// </summary>
    private async Task InsertCompletionRowAsync(
        IReadOnlyList<string> actionLog,
        long totalStamped,
        IReadOnlyDictionary<string, long> perEntityCounts,
        CancellationToken ct)
    {
        var resultPayload = new
        {
            RowsStamped = totalStamped,
            RowsStampedByEntityType = perEntityCounts,
            Actions = actionLog,
        };

        _db.UpgradeHistory.Add(new UpgradeHistoryEntity
        {
            Id = Guid.NewGuid(),
            UpgradeKind = UpgradeKinds.SingleTenantToMultiTenant,
            CompletedAt = DateTimeOffset.UtcNow,
            ActorUserId = "system",
            Message = string.Join(" ", actionLog),
            ResultSummaryJson = JsonSerializer.Serialize(resultPayload, ResultJson),
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
