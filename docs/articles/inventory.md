# Inventory tracking + zero-boundary Polar sync

Polar has no native inventory tracking. PolarSharp stores on-hand counts per variant locally and pushes a `is_archived` flag to Polar **only when a SKU crosses the zero boundary** (in-stock → out-of-stock and back). Ordinary stock decrements (10 → 9 → 8) cost zero Polar API calls.

## Service surface

```csharp
public interface IInventoryUpdater
{
    Task<Result<InventoryUpdateOutcome, PolarError>> UpdateAsync(
        TenantId tenantId,
        VariantId variantId,
        int newCount,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<InventoryUpdateOutcome>, PolarError>> UpdateManyAsync(
        TenantId tenantId,
        IReadOnlyList<InventoryUpdate> updates,
        CancellationToken ct = default);
}

public sealed record InventoryUpdate(VariantId VariantId, int NewCount);
public sealed record InventoryUpdateOutcome(VariantId VariantId, int OldCount, int NewCount, bool CrossedZeroBoundary);
```

The host's existing inventory system (warehouse management, ERP, manual admin form, webhook from a shopping-cart system) calls `UpdateAsync` whenever counts change. PolarSharp keeps the local row up to date; the sync service decides whether Polar needs to know.

## Zero-boundary logic

`SkuStockChanged` is emitted on every call but the sync service acts only when `CrossedZeroBoundary == true`:

| Old count | New count | Crossed | Polar action |
|---|---|---|---|
| 10 | 9 | false | none |
| 1 | 0 | true | PATCH `is_archived: true` on the variant's Polar product |
| 0 | 5 | true | PATCH `is_archived: false` on the variant's Polar product |
| 5 | 4 | false | none |

The sync service is opt-in via `.WithInventoryAutoSync(opts)` — disabled by default so hosts that manage inventory differently (manual archive in Polar dashboard) don't pay for the channel + background task.

## Variant model

`LocalProductVariant` carries `IsActive` / `InventoryCount` / `InventoryLowThreshold` / `IsOutOfStock` / `LastStockChangedAt`. The publisher pushes `is_archived: true` when `IsActive=false` OR `InventoryCount<=0`, and `is_archived: false` when stock returns.

## Bulk reconciliation

`UpdateManyAsync` is the bulk endpoint for nightly reconciliation with an external inventory system. Each row produces an `InventoryUpdateOutcome`; the sync service batches Polar calls into a bounded channel (`QueueCapacity`, default 1000) processed by `MaxConcurrentSyncs` parallel workers (default 4) so a 10k-row reconciliation doesn't slam Polar's API or starve other tenants.

## Configuration

```json
{
  "PolarSharp": {
    "EcommerceStoreManagement": {
      "InventoryAutoSync": {
        "Enabled": false,
        "QueueCapacity": 1000,
        "MaxConcurrentSyncs": 4
      }
    }
  }
}
```

## Hosts who don't want auto-sync

`IInventoryUpdater` still works (count changes persist locally) but no Polar API calls happen. The host can call `IPolarCatalogPublisher.PublishAsync(tenantId, options)` manually on whatever schedule they like — daily, on demand, etc.
