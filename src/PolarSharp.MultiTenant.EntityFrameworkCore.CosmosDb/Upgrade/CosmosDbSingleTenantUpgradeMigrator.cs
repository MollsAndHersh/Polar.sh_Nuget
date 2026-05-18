using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb.Upgrade;

/// <summary>
/// Azure Cosmos DB implementation of <see cref="ISingleTenantUpgradeMigrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>How tenant isolation differs from the relational providers.</strong> Cosmos
/// uses a logical partition key, not a row-level <c>WHERE</c> predicate, to isolate tenants.
/// Every tenant-owned entity is configured with <c>HasPartitionKey(e =&gt; e.TenantId)</c>.
/// "Backfilling" therefore does not map to an in-place column update — Cosmos does not
/// allow the partition key value of an existing item to be changed in place. The migrator
/// instead reads each tenant-naive item, sets its <c>TenantId</c> to the default tenant's
/// id, and re-saves it; EF Core's Cosmos provider issues a <c>ReplaceItem</c> call under
/// the hood. Items that already have a non-empty <c>TenantId</c> are skipped.
/// </para>
/// <para>
/// <strong>RU budget gate.</strong> Cosmos charges Request Units (RUs) for every read,
/// write, and replace. Replacing every tenant-naive item across every container can
/// consume tens of thousands of RUs and show up directly on the operator's bill. The
/// migrator therefore performs a dry-run count pass first and refuses to run when the
/// estimated cost exceeds
/// <see cref="CosmosDbSingleTenantUpgradeOptions.AbortIfEstimatedRuCostExceeds"/> unless
/// the operator has explicitly set
/// <see cref="CosmosDbSingleTenantUpgradeOptions.AcknowledgeCosmosRuCost"/> to
/// <see langword="true"/>. The estimate is a deliberately rough <c>itemCount × 10 RUs</c>
/// — Cosmos replace cost varies by item shape, but 10 RUs per replace is a conservative
/// upper-bound for the small documents PolarSharp ships.
/// </para>
/// <para>
/// <strong>No EF migrations on Cosmos.</strong> Cosmos has no schema migration concept;
/// the upgrade-history container is created on demand via
/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade"/>'s <c>EnsureCreatedAsync</c>. Containers and
/// indexing policies are created the first time the migrator runs.
/// </para>
/// </remarks>
public sealed class CosmosDbSingleTenantUpgradeMigrator : ISingleTenantUpgradeMigrator
{
    /// <summary>Conservative per-replace RU estimate used by the cost-budget gate.</summary>
    /// <remarks>
    /// Cosmos replace cost varies by item shape — 10 RUs is comfortably above the typical
    /// cost for the small registry / catalog documents PolarSharp ships. Operators with
    /// unusually large documents should raise
    /// <see cref="CosmosDbSingleTenantUpgradeOptions.AbortIfEstimatedRuCostExceeds"/> or
    /// set <see cref="CosmosDbSingleTenantUpgradeOptions.AcknowledgeCosmosRuCost"/>.
    /// </remarks>
    public const int EstimatedRuCostPerReplace = 10;

    private static readonly JsonSerializerOptions ResultJson = new()
    {
        WriteIndented = false,
    };

    private readonly PolarTenantDbContext _db;
    private readonly ITenantRegistryUpgrader _registryUpgrader;
    private readonly CosmosDbSingleTenantUpgradeOptions _cosmosOptions;
    private readonly ILogger<CosmosDbSingleTenantUpgradeMigrator> _logger;

    /// <summary>Initializes a new <see cref="CosmosDbSingleTenantUpgradeMigrator"/>.</summary>
    /// <param name="db">The tenant-registry DbContext (also hosts the upgrade-history container).</param>
    /// <param name="registryUpgrader">The provider-agnostic tenant-registry upgrader.</param>
    /// <param name="cosmosOptions">Cosmos-specific knobs (RU budget gate, etc.).</param>
    /// <param name="logger">Logger.</param>
    public CosmosDbSingleTenantUpgradeMigrator(
        PolarTenantDbContext db,
        ITenantRegistryUpgrader registryUpgrader,
        IOptions<CosmosDbSingleTenantUpgradeOptions> cosmosOptions,
        ILogger<CosmosDbSingleTenantUpgradeMigrator> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(registryUpgrader);
        ArgumentNullException.ThrowIfNull(cosmosOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _registryUpgrader = registryUpgrader;
        _cosmosOptions = cosmosOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HasUpgradeCompletedAsync(CancellationToken ct)
    {
        // EnsureCreated covers the bootstrap case where the upgrade-history container does
        // not yet exist — Cosmos has no migrations, so EnsureCreated IS how containers come
        // into being.
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

        var perEntityCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        long totalCandidates = 0;
        foreach (var entityType in EnumerateTenantOwnedEntityTypes(_db))
        {
            var candidates = await CountTenantNaiveItemsAsync(_db, entityType, ct).ConfigureAwait(false);
            perEntityCounts[entityType.ClrType.Name] = candidates;
            totalCandidates += candidates;
        }

        var estimatedRu = totalCandidates * EstimatedRuCostPerReplace;
        if (estimatedRu > _cosmosOptions.AbortIfEstimatedRuCostExceeds
            && !_cosmosOptions.AcknowledgeCosmosRuCost)
        {
            var message =
                $"Estimated RU cost {estimatedRu} (= {totalCandidates} items × " +
                $"{EstimatedRuCostPerReplace} RUs/replace) exceeds the configured threshold " +
                $"{_cosmosOptions.AbortIfEstimatedRuCostExceeds}. Set " +
                $"PolarSharp:MultiTenant:SingleTenantUpgrade:Cosmos:AcknowledgeCosmosRuCost=true " +
                "to proceed, or raise the AbortIfEstimatedRuCostExceeds threshold.";
            _logger.LogError(
                "PolarSharp Cosmos upgrade migrator aborted: {Message}", message);
            return new SingleTenantUpgradeResult
            {
                Success = false,
                AlreadyComplete = false,
                RowsStamped = 0,
                RowsStampedByEntityType = perEntityCounts,
                Duration = sw.Elapsed,
                Message = message,
            };
        }

        var resolvedTenant = await _registryUpgrader.UpsertAsync(defaultTenant, ct).ConfigureAwait(false);
        actionLog.Add(
            $"Resolved default tenant '{resolvedTenant.Identifier}' (Id={resolvedTenant.Id}) in registry.");
        actionLog.Add(
            $"Estimated RU cost {estimatedRu} for {totalCandidates} item(s); proceeding.");

        long totalStamped = 0;
        foreach (var entityType in EnumerateTenantOwnedEntityTypes(_db))
        {
            var stamped = await StampTenantNaiveItemsAsync(
                _db, entityType, resolvedTenant.Id!, ct).ConfigureAwait(false);
            perEntityCounts[entityType.ClrType.Name] = stamped;
            totalStamped += stamped;
            if (stamped > 0)
            {
                actionLog.Add($"Stamped {stamped} item(s) of {entityType.ClrType.Name}.");
            }
        }

        await InsertCompletionRowAsync(actionLog, totalStamped, perEntityCounts, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "PolarSharp Cosmos upgrade migrator: stamped {ItemCount} item(s) across {EntityCount} entity type(s) " +
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
    /// Counts the items on the supplied entity's container whose <c>TenantId</c> is currently
    /// null or empty. Used by the RU-budget gate before any replace work runs.
    /// </summary>
    private static Task<long> CountTenantNaiveItemsAsync(
        PolarTenantDbContext db,
        IEntityType entityType,
        CancellationToken ct)
    {
        return InvokeGenericLongCountAsync(db, entityType.ClrType, ct);
    }

    /// <summary>
    /// Iterates every tenant-naive item on the supplied entity's container, sets its
    /// <c>TenantId</c> property to <paramref name="tenantId"/>, and saves changes via the
    /// Cosmos EF Core provider (which issues an item replace under the hood).
    /// </summary>
    private static async Task<long> StampTenantNaiveItemsAsync(
        PolarTenantDbContext db,
        IEntityType entityType,
        string tenantId,
        CancellationToken ct)
    {
        var tenantIdProperty = entityType.ClrType.GetProperty(
            nameof(ITenantOwned.TenantId),
            BindingFlags.Public | BindingFlags.Instance);
        if (tenantIdProperty is null || !tenantIdProperty.CanWrite)
        {
            // Entity declares ITenantOwned but its TenantId is read-only (e.g., a computed
            // string projection of a Guid field). Stamping such entities requires the
            // application to expose a writable seam — skip rather than fail.
            return 0;
        }

        var items = await InvokeGenericLoadNaiveAsync(db, entityType.ClrType, ct).ConfigureAwait(false);

        long stamped = 0;
        foreach (var item in items)
        {
            tenantIdProperty.SetValue(item, tenantId);
            stamped++;
        }

        if (stamped > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return stamped;
    }

    /// <summary>
    /// Inserts the completion-marker document into <c>polar_upgrade_history</c>.
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

    /// <summary>
    /// Reflection helper: builds <c>db.Set&lt;TEntity&gt;().LongCountAsync(x =&gt; ITenantOwned.TenantId == null || .. == "")</c>.
    /// </summary>
    private static async Task<long> InvokeGenericLongCountAsync(
        PolarTenantDbContext db,
        Type entityClrType,
        CancellationToken ct)
    {
        var method = typeof(CosmosDbSingleTenantUpgradeMigrator)
            .GetMethod(nameof(LongCountNaiveAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityClrType);
        var task = (Task<long>)method.Invoke(null, [db, ct])!;
        return await task.ConfigureAwait(false);
    }

    private static async Task<long> LongCountNaiveAsync<TEntity>(
        PolarTenantDbContext db,
        CancellationToken ct)
        where TEntity : class, ITenantOwned
    {
        return await db.Set<TEntity>()
            .AsNoTracking()
            .LongCountAsync(x => x.TenantId == null || x.TenantId == string.Empty, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reflection helper: loads the tenant-naive items for the supplied CLR type as
    /// tracked entities so that subsequent property writes are persisted on SaveChanges.
    /// </summary>
    private static async Task<IReadOnlyList<object>> InvokeGenericLoadNaiveAsync(
        PolarTenantDbContext db,
        Type entityClrType,
        CancellationToken ct)
    {
        var method = typeof(CosmosDbSingleTenantUpgradeMigrator)
            .GetMethod(nameof(LoadNaiveAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityClrType);
        var task = (Task<IReadOnlyList<object>>)method.Invoke(null, [db, ct])!;
        return await task.ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<object>> LoadNaiveAsync<TEntity>(
        PolarTenantDbContext db,
        CancellationToken ct)
        where TEntity : class, ITenantOwned
    {
        var items = await db.Set<TEntity>()
            .Where(x => x.TenantId == null || x.TenantId == string.Empty)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return items.Cast<object>().ToList();
    }
}
