namespace PolarSharp.EcommerceStoreManagement.Services;

/// <summary>
/// Updates SKU inventory counts. Persists changes locally and emits a
/// <see cref="SkuStockChanged"/> event when the count crosses the zero boundary in either
/// direction — the auto-sync service watches for those events and toggles the variant's
/// <c>is_archived</c> flag in Polar.
/// </summary>
public interface IInventoryUpdater
{
    /// <summary>Updates the on-hand count for a single variant.</summary>
    Task<Result<InventoryUpdateOutcome, InventoryError>> UpdateAsync(
        VariantId variantId,
        int newCount,
        CancellationToken ct = default);

    /// <summary>Bulk-updates inventory for many variants in one call. Used by the host's reconciliation jobs.</summary>
    Task<Result<IReadOnlyList<InventoryUpdateOutcome>, InventoryError>> UpdateManyAsync(
        IReadOnlyList<InventoryUpdate> updates,
        CancellationToken ct = default);
}

/// <summary>One update in a batched inventory adjustment.</summary>
public sealed record InventoryUpdate(VariantId VariantId, int NewCount);

/// <summary>What an inventory update did.</summary>
public sealed record InventoryUpdateOutcome(VariantId VariantId, int OldCount, int NewCount, bool CrossedZeroBoundary);

/// <summary>
/// Domain event emitted on every inventory update — consumed by the auto-sync background
/// service to keep Polar's <c>is_archived</c> flag aligned with local stock state.
/// </summary>
/// <param name="VariantId">The variant whose stock changed.</param>
/// <param name="OldCount">Count before the update.</param>
/// <param name="NewCount">Count after the update.</param>
/// <param name="NowOutOfStock">True iff this change moved the variant from in-stock to out-of-stock.</param>
/// <param name="BackInStock">True iff this change moved the variant from out-of-stock to in-stock.</param>
public sealed record SkuStockChanged(VariantId VariantId, int OldCount, int NewCount, bool NowOutOfStock, bool BackInStock);

/// <summary>Recoverable inventory-update failure modes.</summary>
public sealed record InventoryError(InventoryErrorKind Kind, string Message);

/// <summary>Discriminator for inventory errors.</summary>
public enum InventoryErrorKind
{
    /// <summary>The variant id doesn't exist in the local catalog.</summary>
    VariantNotFound,
    /// <summary>The variant has inventory tracking disabled (<c>InventoryCount</c> is null).</summary>
    InventoryNotTracked,
    /// <summary>Negative count supplied — counts must be &gt;= 0.</summary>
    InvalidCount,
}
