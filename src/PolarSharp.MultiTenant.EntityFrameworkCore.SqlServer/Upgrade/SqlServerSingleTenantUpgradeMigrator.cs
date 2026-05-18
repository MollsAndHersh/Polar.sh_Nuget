using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer.Upgrade;

/// <summary>
/// SQL Server implementation of <see cref="ISingleTenantUpgradeMigrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why SQL Server differs from SQLite.</strong> The SQL Server provider uses a
/// single shared database with rows discriminated by a <c>TenantId</c> column on every
/// <see cref="ITenantOwned"/> entity. The migrator therefore must <em>backfill</em>
/// <c>TenantId</c> on every existing row of every tenant-owned table, not just create the
/// tenant registry entry. The backfill uses EF Core's bulk
/// <see cref="EntityFrameworkQueryableExtensions.ExecuteUpdateAsync{T}"/> so very large
/// tables migrate without loading rows into memory.
/// </para>
/// <para>
/// <strong>RLS session-context handling.</strong> The shipped <c>EnableRowLevelSecurity</c>
/// migration installs a <c>SECURITY POLICY</c> that filters every <c>ITenantOwned</c> table
/// by the <c>SESSION_CONTEXT('tenant_id')</c> session variable. During the backfill the
/// rows being updated have a NULL <c>TenantId</c> by definition — there is no tenant
/// session context yet — so the policy would reject the <c>UPDATE</c> statements. To get
/// the work done, the migrator sets <c>SESSION_CONTEXT('is_app_master_admin') = 1</c> for
/// the duration of the backfill (the same bypass used by <c>[AllowCrossTenant]</c> routes)
/// and resets it in a <c>try / finally</c>. The bypass is intentionally narrower than
/// disabling the policy: it limits the cross-tenant window to the upgrade transaction
/// rather than leaving the policy disabled on a connection that might leak back to the pool.
/// </para>
/// <para>
/// <strong>Transactional posture.</strong> The whole backfill runs in a single transaction
/// so a mid-flight failure rolls back cleanly. The completion-marker insert into
/// <c>polar_upgrade_history</c> is part of the same transaction — operators never see a
/// half-stamped database with no completion record.
/// </para>
/// </remarks>
public sealed class SqlServerSingleTenantUpgradeMigrator : ISingleTenantUpgradeMigrator
{
    private static readonly JsonSerializerOptions ResultJson = new()
    {
        WriteIndented = false,
    };

    private readonly PolarTenantDbContext _db;
    private readonly ITenantRegistryUpgrader _registryUpgrader;
    private readonly ILogger<SqlServerSingleTenantUpgradeMigrator> _logger;

    /// <summary>Initializes a new <see cref="SqlServerSingleTenantUpgradeMigrator"/>.</summary>
    /// <param name="db">The tenant-registry DbContext (also hosts the upgrade-history table).</param>
    /// <param name="registryUpgrader">The provider-agnostic tenant-registry upgrader.</param>
    /// <param name="logger">Logger.</param>
    public SqlServerSingleTenantUpgradeMigrator(
        PolarTenantDbContext db,
        ITenantRegistryUpgrader registryUpgrader,
        ILogger<SqlServerSingleTenantUpgradeMigrator> logger)
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
        // "Invalid object name 'polar_upgrade_history'" instead of returning false.
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
            await SetRlsBypassAsync(_db, enable: true, ct).ConfigureAwait(false);
            try
            {
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
            }
            finally
            {
                await SetRlsBypassAsync(_db, enable: false, ct).ConfigureAwait(false);
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation(
            "PolarSharp SQL Server upgrade migrator: stamped {RowCount} row(s) across {EntityCount} entity type(s) " +
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
    /// Sets (or clears) the <c>is_app_master_admin</c> session-context flag that the shipped
    /// RLS policy honours as a bypass. Wraps <c>sys.sp_set_session_context</c>.
    /// </summary>
    /// <param name="db">The DbContext whose connection is used for the call.</param>
    /// <param name="enable">When <see langword="true"/>, sets the flag to <c>1</c>; otherwise clears it to <c>0</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    private static async Task SetRlsBypassAsync(PolarTenantDbContext db, bool enable, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key=N'is_app_master_admin', @value=@v;";

        var param = cmd.CreateParameter();
        param.ParameterName = "@v";
        param.Value = enable ? 1 : 0;
        cmd.Parameters.Add(param);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
            ? $"[{tableName}]"
            : $"[{schema}].[{tableName}]";

        var sql =
            $"UPDATE {qualifiedTable} SET [TenantId] = @p0 " +
            "WHERE [TenantId] IS NULL OR [TenantId] = '';";
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
