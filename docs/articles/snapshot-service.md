# Report snapshot service

`IReportSnapshotService` mirrors Polar's `/v1/events/`, `/v1/orders/`, `/v1/subscriptions/`, `/v1/customers/`, and `/v1/refunds/` into local SQL on a schedule. Reports then read from indexed local tables instead of paginating Polar every time.

## Why mirror?

Polar's API is rate-limited and paginated. A dashboard that loads "current month MRR" against the live API may paginate hundreds of pages per refresh — slow under load and prone to throttling. The snapshot service pulls deltas in the background; report queries run against fast local SQL with proper indexes (`(tenant_id, last_order_at DESC)` etc.).

## When to enable

| Posture | Snapshot | Reads from |
|---|---|---|
| Production with non-trivial volume (recommended) | `EnableSnapshot=true` | Local indexed SQL |
| Dev / proof-of-concept | `EnableSnapshot=false` | Polar API direct |
| Small tenant, low query rate | either works | configurable |

## Configuration

```json
{
  "PolarSharp": {
    "Reporting": {
      "SnapshotInterval": "00:15:00",
      "MaxTenantsInParallel": 4,
      "EnableSnapshot": true,
      "SnapshotRetentionDays": 365
    }
  }
}
```

`PolarReportingHostedService` (`BackgroundService`) ticks on `PeriodicTimer(SnapshotInterval)`. For each tenant from `IMultiTenantStore<PolarTenantInfo>.GetAllAsync()`, it opens a DI scope, hydrates Finbuckle context via the shared `IPolarTenantScopeInitializer`, and calls `RunSnapshotAsync(tenantId)`. Bounded parallelism (`MaxTenantsInParallel`, default 4) prevents slamming Polar's API across hundreds of tenants.

## Checkpoints

`ReportSnapshotCheckpointEntity` rows per `(tenant_id, resource)` track the cursor: last Polar id ingested, last run timestamp. Idempotent — re-running a tick from the same checkpoint produces no duplicates. A crashed tick resumes from the previous checkpoint.

## Pre-aggregated columns

The snapshot service maintains pre-aggregated columns on `ReportCustomerEntity` (`OrderCount`, `LifetimeValue`, `FirstOrderAt`, `LastOrderAt`) and `ReportOrderEntity` (`LineItemCount`, `RefundedAmount`). These power the hierarchical drilldown's top-level grid without per-row roll-up queries — a tenant with 10k customers loads the first page in tens of milliseconds.

## Retention

`SnapshotRetentionDays` (default 365) bounds disk usage. Older rows are NOT auto-deleted in v1.3.0 — `SnapshotRetentionPruner` is a v2.0 background job (tracked as TASK-V20-009). Operators can run an ad-hoc DELETE today; the schema supports it (every snapshot row has `CreatedAt`).

## v2.0 deferral

`IPolarReportingApi` (the HTTP boundary the snapshot service reads from) is a deferred stub (`PolarClientReportingApi`, TASK-V20-005). Today the impl returns empty pages — an "honest no-op" until the Kiota request builders for `/v1/events/`, `/v1/orders/`, etc. are wired through `PolarClient`. Hosts implementing a custom `IPolarReportingApi` against the live Polar API get real snapshots today.

## What the snapshot does NOT do

- It does **not** call Stripe. PolarSharp does NOT talk to Stripe. Ever. Anywhere.
- It does not modify Polar — read-only HTTP `GET` calls only.
- It does not propagate snapshot data back to Polar — Polar remains the source of truth for the canonical record; the snapshot is a local read-cache.
