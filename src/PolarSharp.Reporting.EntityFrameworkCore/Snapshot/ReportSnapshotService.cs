using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.Snapshot;

namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// Default <see cref="IReportSnapshotService"/> implementation. For each resource (events,
/// orders, subscriptions, customers, benefit grants) it loads the per-tenant checkpoint,
/// pages new rows from Polar, upserts them into local SQL, advances the checkpoint, and
/// finally recomputes the pre-aggregated columns used by the hierarchical drilldown grid.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Idempotency.</strong> Snapshot upserts key on the <c>PolarXxxId</c> wire-format
/// id. Re-running the same checkpoint range produces no duplicates.
/// </para>
/// <para>
/// <strong>Checkpoint semantics.</strong> The checkpoint records the LAST Polar id ingested
/// per resource. The next snapshot run fetches strictly after that id. On a fresh tenant
/// the checkpoint is null and the first run pulls every available row.
/// </para>
/// <para>
/// <strong>Pre-aggregate refresh.</strong> After orders + line items + refunds land, the
/// service recomputes per-customer (<c>OrderCount</c>, <c>LifetimeValue</c>,
/// <c>FirstOrderAt</c>, <c>LastOrderAt</c>) and per-order (<c>LineItemCount</c>,
/// <c>RefundedAmount</c>) aggregates in one bulk pass — these columns back the top-level
/// customer grid and let it load in tens of milliseconds at any tenant scale.
/// </para>
/// <para>
/// Polar HTTP wiring (<see cref="IPolarReportingApi"/>) is best-effort — see TASK-V20-005.
/// Until sandbox-validated, the default <c>PolarClientReportingApi</c> returns empty pages,
/// so snapshot runs are no-ops in production unless hosts supply their own implementation.
/// </para>
/// </remarks>
internal sealed class ReportSnapshotService(
    PolarReportingDbContext db,
    IPolarReportingApi polarApi,
    TimeProvider time,
    ILogger<ReportSnapshotService> logger) : IReportSnapshotService
{
    private const int PageSize = 200;

    private const string ResourceEvents = "events";
    private const string ResourceOrders = "orders";
    private const string ResourceSubscriptions = "subscriptions";
    private const string ResourceCustomers = "customers";
    private const string ResourceBenefitGrants = "benefit_grants";
    // V20-005 Phase 1B+:
    private const string ResourceProducts = "products";
    private const string ResourceCustomerMeters = "customer_meters";
    private const string ResourceLicenseKeys = "license_keys";
    private const string ResourceBenefits = "benefits";
    private const string ResourceMeters = "meters";

    private readonly PolarReportingDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IPolarReportingApi _polarApi = polarApi ?? throw new ArgumentNullException(nameof(polarApi));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ILogger<ReportSnapshotService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<SnapshotReport> RunSnapshotAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        var sw = Stopwatch.StartNew();

        var eventsCount = await IngestEventsAsync(tenantId, ct).ConfigureAwait(false);
        var (ordersCount, lineItemsCount, refundsCount) = await IngestOrdersAsync(tenantId, ct).ConfigureAwait(false);
        var subsCount = await IngestSubscriptionsAsync(tenantId, ct).ConfigureAwait(false);
        var custsCount = await IngestCustomersAsync(tenantId, ct).ConfigureAwait(false);
        var grantsCount = await IngestBenefitGrantsAsync(tenantId, ct).ConfigureAwait(false);
        // V20-005 Phase 1B: products
        var productsCount = await IngestProductsAsync(tenantId, ct).ConfigureAwait(false);
        // V20-005 Phase 1C: customer-meters
        var customerMetersCount = await IngestCustomerMetersAsync(tenantId, ct).ConfigureAwait(false);
        // V20-005 Phase 1D: license-keys
        var licenseKeysCount = await IngestLicenseKeysAsync(tenantId, ct).ConfigureAwait(false);
        // V20-005 Phase 1E: benefits (discriminated union)
        var benefitsCount = await IngestBenefitsAsync(tenantId, ct).ConfigureAwait(false);
        // V20-005 Phase 1F: meters (Aggregation discriminated union)
        var metersCount = await IngestMetersAsync(tenantId, ct).ConfigureAwait(false);

        await RefreshAggregatesAsync(tenantId, ct).ConfigureAwait(false);

        sw.Stop();
        return new SnapshotReport(
            EventsIngested: eventsCount,
            OrdersIngested: ordersCount,
            OrderLineItemsIngested: lineItemsCount,
            OrderRefundsIngested: refundsCount,
            SubscriptionsIngested: subsCount,
            CustomersIngested: custsCount,
            BenefitGrantsIngested: grantsCount,
            ProductsIngested: productsCount,
            CustomerMetersIngested: customerMetersCount,
            LicenseKeysIngested: licenseKeysCount,
            BenefitsIngested: benefitsCount,
            MetersIngested: metersCount,
            Duration: sw.Elapsed);
    }

    // ── Per-resource ingestion ───────────────────────────────────────────────────

    private async Task<int> IngestEventsAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceEvents, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchEventsSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceEvents, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertEventsAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    private async Task<(int Orders, int LineItems, int Refunds)> IngestOrdersAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceOrders, ct).ConfigureAwait(false);
        var orders = 0;
        var lineItems = 0;
        var refunds = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchOrdersSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceOrders, out var rows)) break;
            if (rows.Count == 0) break;

            var (oCount, liCount, rCount) = await UpsertOrdersAsync(tenantId, rows, ct).ConfigureAwait(false);
            orders += oCount;
            lineItems += liCount;
            refunds += rCount;
            cursor = rows[^1].Id;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return (orders, lineItems, refunds);
    }

    private async Task<int> IngestSubscriptionsAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceSubscriptions, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchSubscriptionsSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceSubscriptions, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertSubscriptionsAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    private async Task<int> IngestCustomersAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceCustomers, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchCustomersSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceCustomers, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertCustomersAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    private async Task<int> IngestBenefitGrantsAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceBenefitGrants, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchBenefitGrantsSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceBenefitGrants, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertBenefitGrantsAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    // V20-005 Phase 1B: products ingestion
    private async Task<int> IngestProductsAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceProducts, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchProductsSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceProducts, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertProductsAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    // V20-005 Phase 1C: customer-meters ingestion
    private async Task<int> IngestCustomerMetersAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceCustomerMeters, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchCustomerMetersSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceCustomerMeters, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertCustomerMetersAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    // V20-005 Phase 1D: license-keys ingestion
    private async Task<int> IngestLicenseKeysAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceLicenseKeys, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchLicenseKeysSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceLicenseKeys, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertLicenseKeysAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    // V20-005 Phase 1E: benefits ingestion (discriminated union resource)
    private async Task<int> IngestBenefitsAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceBenefits, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchBenefitsSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceBenefits, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertBenefitsAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    // V20-005 Phase 1F: meters ingestion (Aggregation discriminator on a field)
    private async Task<int> IngestMetersAsync(string tenantId, CancellationToken ct)
    {
        var checkpoint = await LoadOrCreateCheckpointAsync(tenantId, ResourceMeters, ct).ConfigureAwait(false);
        var total = 0;
        string? cursor = checkpoint.LastPolarId;

        while (true)
        {
            var pageResult = await _polarApi.FetchMetersSinceAsync(cursor, PageSize, ct).ConfigureAwait(false);
            if (!TryUnwrapPage(pageResult, ResourceMeters, out var rows)) break;
            if (rows.Count == 0) break;

            await UpsertMetersAsync(tenantId, rows, ct).ConfigureAwait(false);
            cursor = rows[^1].Id;
            total += rows.Count;

            if (rows.Count < PageSize) break;
        }

        await AdvanceCheckpointAsync(checkpoint, cursor, ct).ConfigureAwait(false);
        return total;
    }

    // ── Upserts: idempotent on the Polar wire id ─────────────────────────────────

    private async Task UpsertEventsAsync(string tenantId, IReadOnlyList<EventPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.Events.Where(e => ids.Contains(e.PolarEventId)).ToListAsync(ct).ConfigureAwait(false);
        var existingByPolarId = existing.ToDictionary(e => e.PolarEventId);

        foreach (var row in rows)
        {
            if (existingByPolarId.TryGetValue(row.Id, out var existingRow))
            {
                existingRow.Type = row.Type;
                existingRow.OccurredAt = row.OccurredAt;
                existingRow.PayloadJson = row.PayloadJson;
            }
            else
            {
                _db.Events.Add(new ReportEventEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarEventId = row.Id,
                    Type = row.Type,
                    OccurredAt = row.OccurredAt,
                    PayloadJson = row.PayloadJson,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<(int Orders, int LineItems, int Refunds)> UpsertOrdersAsync(string tenantId, IReadOnlyList<OrderPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.Orders.Where(o => ids.Contains(o.PolarOrderId)).ToListAsync(ct).ConfigureAwait(false);
        var existingByPolarId = existing.ToDictionary(o => o.PolarOrderId);

        var orders = 0;
        var lineItems = 0;
        var refunds = 0;

        foreach (var row in rows)
        {
            ReportOrderEntity entity;
            if (existingByPolarId.TryGetValue(row.Id, out var existingOrder))
            {
                entity = existingOrder;
                entity.OrderNumber = row.Number;
                entity.CustomerId = row.CustomerId;
                entity.Status = row.Status;
                entity.Amount = row.Amount;
                entity.TaxAmount = row.TaxAmount;
                entity.RefundedAmount = row.RefundedAmount;
                entity.Currency = row.Currency;
                entity.LineItemCount = row.LineItems.Count;
                entity.InvoiceUrl = row.InvoiceUrl;
                entity.FulfilledAt = row.FulfilledAt;
            }
            else
            {
                entity = new ReportOrderEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarOrderId = row.Id,
                    OrderNumber = row.Number,
                    CustomerId = row.CustomerId,
                    Status = row.Status,
                    Amount = row.Amount,
                    TaxAmount = row.TaxAmount,
                    RefundedAmount = row.RefundedAmount,
                    Currency = row.Currency,
                    LineItemCount = row.LineItems.Count,
                    InvoiceUrl = row.InvoiceUrl,
                    CreatedAt = row.CreatedAt,
                    FulfilledAt = row.FulfilledAt,
                };
                _db.Orders.Add(entity);
                orders++;
            }

            // Save once so the order has its Guid id available for the child rows.
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            // Replace the order's line items + refunds wholesale on each ingest pass.
            var oldItems = await _db.OrderLineItems.Where(li => li.OrderId == entity.Id).ToListAsync(ct).ConfigureAwait(false);
            _db.OrderLineItems.RemoveRange(oldItems);
            foreach (var li in row.LineItems)
            {
                _db.OrderLineItems.Add(new ReportOrderLineItemEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = entity.Id,
                    ProductId = li.ProductId,
                    ProductName = li.ProductName,
                    PriceId = li.PriceId,
                    Quantity = li.Quantity,
                    UnitAmount = li.UnitAmount,
                    LineTotal = li.LineTotal,
                    DiscountAmount = li.DiscountAmount,
                    TaxAmount = li.TaxAmount,
                });
                lineItems++;
            }

            var oldRefunds = await _db.OrderRefunds.Where(r => r.OrderId == entity.Id).ToListAsync(ct).ConfigureAwait(false);
            _db.OrderRefunds.RemoveRange(oldRefunds);
            foreach (var rf in row.Refunds)
            {
                _db.OrderRefunds.Add(new ReportOrderRefundEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrderId = entity.Id,
                    PolarRefundId = rf.Id,
                    Amount = rf.Amount,
                    Currency = rf.Currency,
                    Reason = rf.Reason,
                    CreatedAt = rf.CreatedAt,
                });
                refunds++;
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return (orders, lineItems, refunds);
    }

    private async Task UpsertSubscriptionsAsync(string tenantId, IReadOnlyList<SubscriptionPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.Subscriptions.Where(s => ids.Contains(s.PolarSubscriptionId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(s => s.PolarSubscriptionId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.Id, out var s))
            {
                s.CustomerId = row.CustomerId;
                s.ProductId = row.ProductId;
                s.Status = row.Status;
                s.CanceledAt = row.CanceledAt;
            }
            else
            {
                _db.Subscriptions.Add(new ReportSubscriptionEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarSubscriptionId = row.Id,
                    CustomerId = row.CustomerId,
                    ProductId = row.ProductId,
                    Status = row.Status,
                    StartedAt = row.StartedAt,
                    CanceledAt = row.CanceledAt,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task UpsertCustomersAsync(string tenantId, IReadOnlyList<CustomerPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.Customers.Where(c => ids.Contains(c.PolarCustomerId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(c => c.PolarCustomerId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.Id, out var c))
            {
                c.Email = row.Email;
                c.Name = row.Name;
                c.Currency = row.Currency;
            }
            else
            {
                _db.Customers.Add(new ReportCustomerEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarCustomerId = row.Id,
                    Email = row.Email,
                    Name = row.Name,
                    Currency = row.Currency,
                    CreatedAt = row.CreatedAt,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task UpsertBenefitGrantsAsync(string tenantId, IReadOnlyList<BenefitGrantPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.BenefitGrants.Where(g => ids.Contains(g.PolarGrantId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(g => g.PolarGrantId);

        foreach (var row in rows)
        {
            // Resolve OrderId guid from the order's Polar id (when present).
            Guid? localOrderId = null;
            if (row.OrderId is { Length: > 0 } orderPolarId)
            {
                var matchedOrder = await _db.Orders.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.PolarOrderId == orderPolarId, ct).ConfigureAwait(false);
                localOrderId = matchedOrder?.Id;
            }

            if (byId.TryGetValue(row.Id, out var g))
            {
                g.CustomerId = row.CustomerId;
                g.OrderId = localOrderId;
                g.BenefitName = row.BenefitName;
                g.BenefitKind = row.BenefitKind;
                g.IsGranted = row.IsGranted;
            }
            else
            {
                _db.BenefitGrants.Add(new ReportBenefitGrantEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarGrantId = row.Id,
                    CustomerId = row.CustomerId,
                    OrderId = localOrderId,
                    BenefitId = row.BenefitId,
                    BenefitName = row.BenefitName,
                    BenefitKind = row.BenefitKind,
                    IsGranted = row.IsGranted,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // V20-005 Phase 1F: meters upsert — idempotent on PolarMeterId
    private async Task UpsertMetersAsync(string tenantId, IReadOnlyList<MeterPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.Meters.Where(m => ids.Contains(m.PolarMeterId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(m => m.PolarMeterId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.Id, out var m))
            {
                m.Name = row.Name;
                m.AggregationKind = row.AggregationKind;
            }
            else
            {
                _db.Meters.Add(new ReportMeterEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarMeterId = row.Id,
                    Name = row.Name,
                    AggregationKind = row.AggregationKind,
                    CreatedAt = row.CreatedAt,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // V20-005 Phase 1E: benefits upsert — idempotent on PolarBenefitId
    private async Task UpsertBenefitsAsync(string tenantId, IReadOnlyList<BenefitPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.Benefits.Where(b => ids.Contains(b.PolarBenefitId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(b => b.PolarBenefitId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.Id, out var b))
            {
                b.Name = row.Name;
                b.Kind = row.Kind;
                b.Description = row.Description;
                b.IsActive = row.IsActive;
                b.ModifiedAt = row.ModifiedAt;
            }
            else
            {
                _db.Benefits.Add(new ReportBenefitEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarBenefitId = row.Id,
                    Name = row.Name,
                    Kind = row.Kind,
                    Description = row.Description,
                    IsActive = row.IsActive,
                    CreatedAt = row.CreatedAt,
                    ModifiedAt = row.ModifiedAt,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // V20-005 Phase 1D: license-keys upsert — idempotent on PolarLicenseKeyId
    private async Task UpsertLicenseKeysAsync(string tenantId, IReadOnlyList<LicenseKeyPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.LicenseKeys.Where(lk => ids.Contains(lk.PolarLicenseKeyId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(lk => lk.PolarLicenseKeyId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.Id, out var lk))
            {
                lk.CustomerId = row.CustomerId;
                lk.BenefitId = row.BenefitId;
                lk.DisplayKey = row.DisplayKey;
                lk.Status = row.Status;
                lk.LimitActivations = row.LimitActivations;
                lk.ActivationsUsed = row.ActivationsUsed;
                lk.ExpiresAt = row.ExpiresAt;
            }
            else
            {
                _db.LicenseKeys.Add(new ReportLicenseKeyEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarLicenseKeyId = row.Id,
                    CustomerId = row.CustomerId,
                    BenefitId = row.BenefitId,
                    DisplayKey = row.DisplayKey,
                    Status = row.Status,
                    LimitActivations = row.LimitActivations,
                    ActivationsUsed = row.ActivationsUsed,
                    ExpiresAt = row.ExpiresAt,
                    CreatedAt = row.CreatedAt,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // V20-005 Phase 1C: customer-meters upsert — idempotent on PolarCustomerMeterId
    private async Task UpsertCustomerMetersAsync(string tenantId, IReadOnlyList<CustomerMeterPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.CustomerMeters.Where(cm => ids.Contains(cm.PolarCustomerMeterId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(cm => cm.PolarCustomerMeterId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.Id, out var cm))
            {
                cm.CustomerId = row.CustomerId;
                cm.MeterId = row.MeterId;
                cm.ConsumedUnits = row.ConsumedUnits;
                cm.CreditedUnits = row.CreditedUnits;
                cm.ModifiedAt = row.ModifiedAt;
            }
            else
            {
                _db.CustomerMeters.Add(new ReportCustomerMeterEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarCustomerMeterId = row.Id,
                    CustomerId = row.CustomerId,
                    MeterId = row.MeterId,
                    ConsumedUnits = row.ConsumedUnits,
                    CreditedUnits = row.CreditedUnits,
                    CreatedAt = row.CreatedAt,
                    ModifiedAt = row.ModifiedAt,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // V20-005 Phase 1B: products upsert — idempotent on PolarProductId
    private async Task UpsertProductsAsync(string tenantId, IReadOnlyList<ProductPayload> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var existing = await _db.Products.Where(p => ids.Contains(p.PolarProductId)).ToListAsync(ct).ConfigureAwait(false);
        var byId = existing.ToDictionary(p => p.PolarProductId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.Id, out var p))
            {
                p.Name = row.Name;
                p.Description = row.Description;
                p.IsRecurring = row.IsRecurring;
                p.RecurringInterval = row.RecurringInterval;
                p.IsArchived = row.IsArchived;
                p.ModifiedAt = row.ModifiedAt;
            }
            else
            {
                _db.Products.Add(new ReportProductEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PolarProductId = row.Id,
                    Name = row.Name,
                    Description = row.Description,
                    IsRecurring = row.IsRecurring,
                    RecurringInterval = row.RecurringInterval,
                    IsArchived = row.IsArchived,
                    CreatedAt = row.CreatedAt,
                    ModifiedAt = row.ModifiedAt,
                });
            }
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Pre-aggregate refresh ────────────────────────────────────────────────────

    private async Task RefreshAggregatesAsync(string tenantId, CancellationToken ct)
    {
        // Per-customer aggregates from the orders table. SQLite cannot Min/Max a
        // DateTimeOffset server-side, so we materialise the order rows and group in memory.
        // Tenant order volumes are bounded; this is fine for snapshot run cadence.
        var orderRowsForAgg = await _db.Orders.AsNoTracking()
            .Select(o => new { o.CustomerId, o.Amount, o.RefundedAmount, o.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);

        var perCustomer = orderRowsForAgg
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                OrderCount = g.Count(),
                LifetimeValue = g.Sum(o => o.Amount - o.RefundedAmount),
                FirstOrderAt = g.Min(o => o.CreatedAt),
                LastOrderAt = g.Max(o => o.CreatedAt),
            })
            .ToList();

        var customers = await _db.Customers.ToListAsync(ct).ConfigureAwait(false);
        var perCustomerIndex = perCustomer.ToDictionary(p => p.CustomerId);
        foreach (var c in customers)
        {
            if (perCustomerIndex.TryGetValue(c.PolarCustomerId, out var agg))
            {
                c.OrderCount = agg.OrderCount;
                c.LifetimeValue = agg.LifetimeValue;
                c.FirstOrderAt = agg.FirstOrderAt;
                c.LastOrderAt = agg.LastOrderAt;
            }
            else
            {
                c.OrderCount = 0;
                c.LifetimeValue = 0;
            }
        }

        // Per-order line-item count + refunded amount.
        var perOrderLineItems = await _db.OrderLineItems.AsNoTracking()
            .GroupBy(li => li.OrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);
        var perOrderRefunds = await _db.OrderRefunds.AsNoTracking()
            .GroupBy(r => r.OrderId)
            .Select(g => new { OrderId = g.Key, Total = g.Sum(r => r.Amount) })
            .ToListAsync(ct).ConfigureAwait(false);

        var orders = await _db.Orders.ToListAsync(ct).ConfigureAwait(false);
        var liIndex = perOrderLineItems.ToDictionary(p => p.OrderId);
        var rfIndex = perOrderRefunds.ToDictionary(p => p.OrderId);
        foreach (var o in orders)
        {
            o.LineItemCount = liIndex.TryGetValue(o.Id, out var li) ? li.Count : 0;
            o.RefundedAmount = rfIndex.TryGetValue(o.Id, out var rf) ? rf.Total : 0;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogDebug("Snapshot pre-aggregate refresh complete for tenant {TenantId}: {Customers} customers, {Orders} orders.", tenantId, customers.Count, orders.Count);
    }

    // ── Checkpoint helpers ───────────────────────────────────────────────────────

    private async Task<ReportSnapshotCheckpointEntity> LoadOrCreateCheckpointAsync(string tenantId, string resource, CancellationToken ct)
    {
        var checkpoint = await _db.Checkpoints
            .FirstOrDefaultAsync(c => c.Resource == resource, ct)
            .ConfigureAwait(false);
        if (checkpoint is null)
        {
            checkpoint = new ReportSnapshotCheckpointEntity
            {
                TenantId = tenantId,
                Resource = resource,
                LastPolarId = null,
                LastRunAt = _time.GetUtcNow(),
            };
            _db.Checkpoints.Add(checkpoint);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        return checkpoint;
    }

    private async Task AdvanceCheckpointAsync(ReportSnapshotCheckpointEntity checkpoint, string? lastId, CancellationToken ct)
    {
        checkpoint.LastPolarId = lastId;
        checkpoint.LastRunAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private bool TryUnwrapPage<T>(Result<IReadOnlyList<T>, PolarReportingApiError> result, string resource, out IReadOnlyList<T> rows)
    {
        if (result.IsFailure)
        {
            var err = result.Match(onSuccess: _ => throw new InvalidOperationException(), onFailure: e => e);
            _logger.LogWarning("Snapshot: {Resource} fetch failed: {Kind} — {Message}. Skipping remaining pages.", resource, err.Kind, err.Message);
            rows = [];
            return false;
        }
        rows = result.Match(onSuccess: r => r, onFailure: _ => throw new InvalidOperationException());
        return true;
    }
}
