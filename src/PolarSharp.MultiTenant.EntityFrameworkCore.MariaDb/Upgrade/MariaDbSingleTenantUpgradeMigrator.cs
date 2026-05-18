using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb.Upgrade;

/// <summary>
/// MariaDB / MySQL implementation of <see cref="ISingleTenantUpgradeMigrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why MariaDB is simpler than SQL Server / Postgres.</strong> MariaDB and MySQL
/// do not expose Postgres-style <c>ROW LEVEL SECURITY</c>, so the shipped tenant isolation
/// is enforced by the EF Core global query filter alone — there is no database-level
/// policy to bypass during the backfill. The migrator therefore does not have to
/// manipulate any session variables.
/// </para>
/// <para>
/// <strong>What the migrator does.</strong> The MariaDB provider uses a single shared
/// database with rows discriminated by a <c>TenantId</c> column on every
/// <see cref="ITenantOwned"/> entity. The migrator iterates every tenant-owned entity
/// type in the model and issues a bulk <c>UPDATE</c> via
/// <see cref="RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade, string, object[])"/>
/// so very large tables migrate without loading rows into memory. The whole backfill
/// runs in a single transaction so a mid-flight failure rolls back cleanly.
/// </para>
/// <para>
/// <strong>Defense-in-depth posture.</strong> Because MariaDB has no DB-layer tenant
/// isolation, a bug that bypasses the DbContext entirely (e.g. raw <c>IDbConnection</c>
/// queries) cannot be caught by a database policy on this provider. Hosts requiring
/// defense in depth at the DB layer should choose the Postgres or SQL Server provider.
/// </para>
/// </remarks>
public sealed class MariaDbSingleTenantUpgradeMigrator : ISingleTenantUpgradeMigrator
{
    private static readonly JsonSerializerOptions ResultJson = new()
    {
        WriteIndented = false,
    };

    private readonly PolarTenantDbContext _db;
    private readonly ITenantRegistryUpgrader _registryUpgrader;
    private readonly ILogger<MariaDbSingleTenantUpgradeMigrator> _logger;

    /// <summary>Initializes a new <see cref="MariaDbSingleTenantUpgradeMigrator"/>.</summary>
    /// <param name="db">The tenant-registry DbContext (also hosts the upgrade-history table).</param>
    /// <param name="registryUpgrader">The provider-agnostic tenant-registry upgrader.</param>
    /// <param name="logger">Logger.</param>
    public MariaDbSingleTenantUpgradeMigrator(
        PolarTenantDbContext db,
        ITenantRegistryUpgrader registryUpgrader,
        ILogger<MariaDbSingleTenantUpgradeMigrator> logger)
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
        // "Table 'polar_upgrade_history' doesn't exist" instead of returning false.
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
            "PolarSharp MariaDB upgrade migrator: stamped {RowCount} row(s) across {EntityCount} entity type(s) " +
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
        var qualifiedTable = $"`{tableName}`";

        var sql =
            $"UPDATE {qualifiedTable} SET `TenantId` = {{0}} " +
            "WHERE `TenantId` IS NULL OR `TenantId` = '';";
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
