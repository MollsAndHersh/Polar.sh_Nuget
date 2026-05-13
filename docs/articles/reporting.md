# Reporting

`PolarSharp.Reporting` exposes per-tenant analytics over Polar's order, subscription, customer, refund, and event data. Two flavours of API:

- **Aggregate / KPI** reports — single roll-up for a dashboard tile (transactions, subscriptions, orders, customers, error audit, customer entitlements)
- **Hierarchical drilldown** — three lazy methods designed for Telerik / MudBlazor / Blazor hierarchical grids

Plus JSON variants of every aggregate method (return pre-serialised JSON — no double round-trip through DTO mapping).

## Aggregate / KPI reports

```csharp
var result = await client.GetTransactionsAsync(new TransactionReportRequest
{
    PeriodStart = DateTimeOffset.UtcNow.AddMonths(-1),
    PeriodEnd = DateTimeOffset.UtcNow,
    Currency = "USD",
    Granularity = TimeBucketGranularity.Daily,
});
```

Returns `TransactionReport` with `GrossRevenue` / `RefundedAmount` / `NetRevenue` / `OrderCount` / `AverageOrderValue` / `TopProducts` / `TimeBuckets`.

## Hierarchical drilldown

Three lazy methods that map to a hierarchical grid's expand-on-demand pattern:

```csharp
// Top level — paged customers grid with pre-aggregated columns
var customers = await client.ListCustomersAsync(new CustomerListRequest { Page = 0, PageSize = 50 });

// Mid level — invoked when operator expands a customer row
var orders = await client.ListOrdersForCustomerAsync(customerId, new OrderListRequest { Page = 0 });

// Bottom level — invoked when operator opens a specific order
var detail = await client.GetOrderDrilldownAsync(orderId);
// detail.LineItems, detail.Refunds, detail.BenefitGrants all populated in one call
```

### Why three methods, not one nested shape

An eager nested shape (`CustomersWithOrdersWithLineItems`) sized for a tenant with 10k customers OOMs the host and ships the entire dataset over JSON every page load. Telerik's hierarchical grids fetch detail rows on-demand precisely so they DON'T do this. Lazy fetching aligns with how dashboards actually behave: the operator views one or two customers at a time and opens at most a handful of orders. Memory + Polar API cost scales with what's viewed, not with the total table.

### Pre-aggregated columns

`CustomerListRow` carries `OrderCount`, `LifetimeValue`, `FirstOrderAt`, `LastOrderAt`. `OrderSummaryRow` carries `LineItemCount`, `RefundedAmount`. These columns are maintained on the snapshot tables on every snapshot tick, so the top-level grid renders without per-row roll-up queries — even on tenants with 10k+ customers loading the first page in tens of milliseconds.

## Snapshot vs Polar-API mode

When the optional `IReportSnapshotService` is enabled (`PolarSharp:Reporting:EnableSnapshot=true`), reports read from local indexed SQL — the snapshot service mirrors Polar's `/v1/events/` / `/v1/orders/` / `/v1/subscriptions/` / `/v1/customers/` into local tables on a schedule (default every 15 minutes). When the service is off, reports fall back to live Polar API calls with PolarSharp's pagination + circuit breaker. **Production posture: enable snapshots.**

## Export

`IReportExporter.ExportCsvAsync<T>(rows, stream)` / `ExportJsonAsync<T>(...)` stream rows directly to a `Stream` — never buffers the whole result in memory. Use from the host's "download report" endpoint.

## Snapshot schema

8 EF entities back the reporting layer: `ReportEventEntity`, `ReportOrderEntity`, `ReportOrderLineItemEntity`, `ReportOrderRefundEntity`, `ReportSubscriptionEntity`, `ReportCustomerEntity`, `ReportBenefitGrantEntity`, `ReportSnapshotCheckpointEntity` (the per-tenant per-resource ingestion cursor). Indexes are tuned for the drilldown paging pattern — `(TenantId, LastOrderAt)` on customers, `(TenantId, CustomerId, CreatedAt)` on orders, `(TenantId, OrderId)` on line items / refunds / grants.
