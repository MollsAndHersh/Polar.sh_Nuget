using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Services;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IInventoryUpdater"/> implementation. Persists count changes in the
/// local catalog and publishes a <see cref="SkuStockChanged"/> event on every update so the
/// auto-sync hosted service can decide whether to PATCH <c>is_archived</c> on Polar.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Zero-boundary semantics</strong>: <see cref="InventoryUpdateOutcome.CrossedZeroBoundary"/>
/// flips only when the count crosses from positive to zero-or-below, OR from zero-or-below
/// to positive. Routine in-stock decrements (10 → 9) do NOT cross. The sync service uses
/// this flag to avoid spamming Polar's API on every minor stock change.
/// </para>
/// <para>
/// <strong>Variants with inventory disabled</strong> (entity.InventoryCount = null) cannot
/// be updated through this service — return <see cref="InventoryErrorKind.InventoryNotTracked"/>.
/// Enable tracking by setting an initial count via direct DbContext access first.
/// </para>
/// </remarks>
internal sealed class InventoryUpdater(
    PolarCatalogDbContext db,
    IInventoryEventNotifier notifier,
    TimeProvider time,
    ILogger<InventoryUpdater> logger) : IInventoryUpdater
{
    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IInventoryEventNotifier _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ILogger<InventoryUpdater> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<InventoryUpdateOutcome, InventoryError>> UpdateAsync(
        VariantId variantId,
        int newCount,
        CancellationToken ct = default)
    {
        if (newCount < 0)
            return Result<InventoryUpdateOutcome, InventoryError>.Failure(new InventoryError(
                InventoryErrorKind.InvalidCount, $"Inventory count must be >= 0 (got {newCount})."));

        var entity = await _db.Variants.FirstOrDefaultAsync(v => v.Id == variantId.Value, ct).ConfigureAwait(false);
        if (entity is null)
            return Result<InventoryUpdateOutcome, InventoryError>.Failure(new InventoryError(
                InventoryErrorKind.VariantNotFound, $"Variant '{variantId.Value}' not found."));

        if (entity.InventoryCount is null)
            return Result<InventoryUpdateOutcome, InventoryError>.Failure(new InventoryError(
                InventoryErrorKind.InventoryNotTracked,
                $"Variant '{variantId.Value}' has inventory tracking disabled (InventoryCount is null). Set an initial count via direct DbContext access first."));

        var oldCount = entity.InventoryCount.Value;
        entity.InventoryCount = newCount;
        entity.LastStockChangedAt = _time.GetUtcNow();

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        var outcome = BuildOutcome(variantId, oldCount, newCount);
        PublishEvent(outcome, variantId, oldCount, newCount);
        return Result<InventoryUpdateOutcome, InventoryError>.Success(outcome);
    }

    public async Task<Result<IReadOnlyList<InventoryUpdateOutcome>, InventoryError>> UpdateManyAsync(
        IReadOnlyList<InventoryUpdate> updates,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
            return Result<IReadOnlyList<InventoryUpdateOutcome>, InventoryError>.Success([]);

        foreach (var u in updates)
            if (u.NewCount < 0)
                return Result<IReadOnlyList<InventoryUpdateOutcome>, InventoryError>.Failure(new InventoryError(
                    InventoryErrorKind.InvalidCount, $"Inventory count must be >= 0 (got {u.NewCount} for variant {u.VariantId.Value})."));

        var variantIds = updates.Select(u => u.VariantId.Value).Distinct().ToList();
        var entities = await _db.Variants.Where(v => variantIds.Contains(v.Id)).ToListAsync(ct).ConfigureAwait(false);
        var entityIndex = entities.ToDictionary(e => e.Id);

        var outcomes = new List<InventoryUpdateOutcome>(updates.Count);
        var stagedEvents = new List<(InventoryUpdateOutcome outcome, VariantId vid, int oldCount, int newCount)>(updates.Count);

        foreach (var update in updates)
        {
            if (!entityIndex.TryGetValue(update.VariantId.Value, out var entity))
                return Result<IReadOnlyList<InventoryUpdateOutcome>, InventoryError>.Failure(new InventoryError(
                    InventoryErrorKind.VariantNotFound, $"Variant '{update.VariantId.Value}' not found."));

            if (entity.InventoryCount is null)
                return Result<IReadOnlyList<InventoryUpdateOutcome>, InventoryError>.Failure(new InventoryError(
                    InventoryErrorKind.InventoryNotTracked, $"Variant '{update.VariantId.Value}' has inventory tracking disabled."));

            var oldCount = entity.InventoryCount.Value;
            entity.InventoryCount = update.NewCount;
            entity.LastStockChangedAt = _time.GetUtcNow();
            var outcome = BuildOutcome(update.VariantId, oldCount, update.NewCount);
            outcomes.Add(outcome);
            stagedEvents.Add((outcome, update.VariantId, oldCount, update.NewCount));
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        foreach (var (outcome, vid, oldCount, newCount) in stagedEvents)
            PublishEvent(outcome, vid, oldCount, newCount);

        return Result<IReadOnlyList<InventoryUpdateOutcome>, InventoryError>.Success(outcomes);
    }

    private static InventoryUpdateOutcome BuildOutcome(VariantId variantId, int oldCount, int newCount)
    {
        var wasInStock = oldCount > 0;
        var isInStock = newCount > 0;
        var crossedZero = wasInStock != isInStock;
        return new InventoryUpdateOutcome(variantId, oldCount, newCount, crossedZero);
    }

    private void PublishEvent(InventoryUpdateOutcome outcome, VariantId variantId, int oldCount, int newCount)
    {
        if (!outcome.CrossedZeroBoundary)
            return;

        var nowOutOfStock = oldCount > 0 && newCount <= 0;
        var backInStock = oldCount <= 0 && newCount > 0;
        var ev = new SkuStockChanged(variantId, oldCount, newCount, nowOutOfStock, backInStock);
        if (!_notifier.TryNotify(ev))
        {
            _logger.LogWarning(
                "InventoryUpdater: notifier dropped SkuStockChanged event for variant {VariantId} ({OldCount} → {NewCount}). Auto-sync may miss this transition.",
                variantId.Value, oldCount, newCount);
        }
    }
}

/// <summary>
/// Channel-backed publisher of <see cref="SkuStockChanged"/> events. The auto-sync hosted
/// service reads from the same channel; this seam lets us test the
/// <see cref="InventoryUpdater"/> publishing logic without spinning up the consumer.
/// </summary>
public interface IInventoryEventNotifier
{
    /// <summary>Publishes an event. Returns <see langword="false"/> if the channel is at capacity (caller should log).</summary>
    bool TryNotify(SkuStockChanged change);
}

/// <summary>Default <see cref="IInventoryEventNotifier"/> backed by a bounded <c>Channel&lt;SkuStockChanged&gt;</c>.</summary>
public sealed class ChannelInventoryEventNotifier(ChannelWriter<SkuStockChanged> writer) : IInventoryEventNotifier
{
    private readonly ChannelWriter<SkuStockChanged> _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    /// <inheritdoc/>
    public bool TryNotify(SkuStockChanged change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return _writer.TryWrite(change);
    }
}
