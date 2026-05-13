# PolarSharp Production-Readiness Analysis

**Version targeted:** v1.3.0 pre-release
**Date:** 2026-05-13
**Audience:** Engineers planning v1.4 / v2.0, plus the SaaS founder who needs to know what is and isn't bulletproof yet.

This document is the answer to the question *"what is missing that would make PolarSharp more useful, bulletproof and performant?"*. It was written after walking the actual code in `src/` and the migrations in each provider package — not from the plan docs alone. Each gap is rated **P1** (production blocker), **P2** (should fix before scaling beyond a few tenants), or **P3** (nice-to-have polish), and each gap proposes a `TASK-V20-NNN` entry that can be dropped into `~/TASKS.md`.

There is a lot of good news mixed in here. The honest framing for the closing section first: PolarSharp at v1.3.0 is genuinely impressive — strong isolation primitives, a clean Result monad, per-tenant bulkheads, an idempotency-key handler, AOT-compatible JSON, source-linked NuGet packaging, and 379+ green tests. The list of gaps below is long because the project is large, not because the project is shaky.

---

## 1. Resilience and reliability gaps

### 1.1 Idempotency-Key is auto-generated *per call*, not *per logical retry*

**File:** `src/PolarSharp/Idempotency/IdempotencyKeyHandler.cs:38-44`

The handler attaches an `X-Idempotency-Key` only if one is not already present, and generates a fresh GUID when missing. Because the Polly retry strategy lives **outside** this handler in the per-tenant pipeline (`MultiTenantPolarClientFactory.BuildTenantResiliencePipeline`), Polly *re-sends the same `HttpRequestMessage`* on retry — so the same key does get reused within one Polly attempt chain. Good.

The remaining risk: **callers cannot supply their own idempotency key** at the typed-client surface. The header is opaque to the Kiota-generated `PolarClient` API. If two PolarSharp processes (blue/green deploys, or a host-level retry on a higher layer) attempt the same logical operation, they will mint different keys and Polar will see two distinct write requests.

- **Severity:** P2 (real risk during deployments, refunds, publishes)
- **Remediation:** Surface an `IIdempotencyKeyProvider` abstraction that callers can populate per logical operation (e.g. seeded from the `OrderId` for a refund), and have `IdempotencyKeyHandler` consult it before generating a random key.
- **Proposed task:** `TASK-V20-010 — Caller-supplied idempotency keys on write paths`

### 1.2 No transaction boundary on multi-step publishes

**File:** `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore/Publishing/PolarCatalogPublisher.cs:73-121`

`PublishAsync` walks Benefits → Products → Discounts → CheckoutLinks, mutating each entity's `PolarXxxId`, `Status`, `LastPublishedAt` along the way, then calls a single `SaveChangesAsync` at the end (line 102). If the publisher itself crashes between, say, "successfully created Polar benefit #7" and "save EF state changes", the new `PolarBenefitId` is **lost** — the next run will create a duplicate benefit in Polar. The Polar-side idempotency key (above) would prevent a *duplicate*, but the local entity is permanently desynchronized.

There is no `using var tx = await _db.Database.BeginTransactionAsync()`. No outbox table either — `grep -r "BeginTransaction" src` returns zero matches across the whole codebase.

- **Severity:** P1 once the Polar HTTP wrapper is live (TASK-V20-001). Currently masked because `PolarClientPublishingApi` is a stub.
- **Remediation:** Wrap each `(api call → state mutation)` pair in a `(persist outcome | mark-failed)` block with an outbox row, or stream `SaveChangesAsync` after every action (slower but durable). Pick the outbox if you want write-amplification to stay low.
- **Proposed task:** `TASK-V20-011 — Outbox pattern for catalog publish state mutations`

### 1.3 Variant expansion is a single plan action — partial failure across variants is silent

**File:** `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore/Publishing/PolarCatalogPublisher.cs:202-217`

A product with `HasVariants=true` produces ONE `PlannedAction` but the comment on the class (line 31-34) states "ONE plan action but multiple Polar products at execution time (one per variant)". The execution path `PublishProductCreateAsync` (line 318) sends *one* payload via `_polarApi.CreateProductAsync`. There is no loop over variants. So either (a) the comment is wrong and variants currently don't get individual Polar products, or (b) the production HTTP impl is expected to fan out internally — which has no transactional rollback.

- **Severity:** P1 (a real product-management correctness gap)
- **Remediation:** Expand variants into N `CreatePolarProduct` actions at plan time; track per-variant outcomes so a partial variant failure doesn't leave half-published products invisible.
- **Proposed task:** `TASK-V20-012 — Plan-time variant expansion with per-variant outcomes`

### 1.4 Background webhook queue has no dead-letter store

**File:** `src/PolarSharp.Webhooks/BackgroundQueue/PolarWebhookBackgroundService.cs:62-74`

On unhandled handler exception, `ProcessSafeAsync` logs and *moves on*. The event is gone. Lines 96-102 explicitly note "events were dropped" on graceful drain timeout. `PolarWebhookHandlerBase.OnErrorAsync` (`src/PolarSharp.Webhooks/PolarWebhookHandlerBase.cs:150`) is a `virtual` no-op the consumer must override — the SDK ships no built-in DLQ.

Combined with the bounded in-memory dedup store (`WebhookInMemoryDedupService.cs`), a multi-instance host that misses an event has no way to replay it from PolarSharp. The reconciliation reader (`PolarWebhookReconciler.cs`) is the partial backstop, but it requires Polar's delivery API to be available — and it isn't gated on "did the handler succeed".

- **Severity:** P1 for billing-relevant events (refunds, subscriptions)
- **Remediation:** Ship an EF-backed `IWebhookDeadLetterStore` writing failed events into a `polar_webhook_dead_letters` table; expose `IPolarWebhookReplayService` that the operator UI can drive. Connect to the existing reconciliation flow.
- **Proposed task:** `TASK-V20-013 — Webhook dead-letter store and operator replay UI`

### 1.5 FakeDataSyncService does NOT survive process restart

**File:** `src/PolarSharp.DataSeeding/Sync/FakeDataSyncService.cs:33-46, 67-86`

The toggle-change events live in a single `Channel<FakeDataToggleChanged>` registered as a DI singleton. If the host crashes between a merchant flipping `AllowFakeData=true` and the sync running, **the event is gone** — there is no persistent queue, no checkpoint, and `PolarDataSeedingOptions.ToggleChannelCapacity` is 256 events.

Also, the implementation in lines 99-108 logs "deferred to Phase 11" — meaning the production sync isn't wired up yet anyway. But the durability hole is real for v1.4 onwards.

- **Severity:** P2 (event is replayable from the catalog state once Phase 11 lands)
- **Remediation:** Persist toggle events to a `polar_fake_data_sync_pending` table with `(tenantId, newValue, occurredAt, processedAt)`; reload pending rows on startup.
- **Proposed task:** `TASK-V20-014 — Durable fake-data toggle queue`

### 1.6 Circuit-breaker tuning is identical across all tenants

**File:** `src/PolarSharp.MultiTenant/MultiTenantPolarClientFactory.cs:101-123`

Every tenant gets `FailureRatio=0.5, MinimumThroughput=5, SamplingDuration=30s, BreakDuration=15s` baked into the factory. A high-volume tenant with 50 RPS hits 5 failures in milliseconds and trips; a low-volume tenant with 2 RPS barely reaches `MinimumThroughput`. There is no config surface for per-tenant override.

- **Severity:** P3 (the defaults are reasonable for both ends; not a blocker)
- **Remediation:** Expose `PolarMultiTenantOptions.CircuitBreaker` and let the per-tenant `PolarTenantInfo` override it.
- **Proposed task:** `TASK-V20-015 — Per-tenant circuit-breaker tuning`

### 1.7 Onboarding has an inconsistent-state hole between OAT mint and webhook registration

**File:** `src/PolarSharp.Onboarding/PolarOnboardingClient.cs:56-75`

If `CreateWebhookEndpointAsync` fails after the OAT was minted (line 57), the code returns an error but the OAT and organization remain alive in Polar. The error message acknowledges this ("the host should either retry webhook registration or archive the organization") — but there is no automatic cleanup, no retry-with-backoff, and no follow-up. A flake at this exact point creates a Polar-side orphan organization that PolarSharp will not register a tenant for, so the merchant cannot re-onboard with the same slug.

- **Severity:** P2
- **Remediation:** Retry webhook registration N times with exponential backoff inside the orchestrator; on terminal failure, call a `CompensatingAction.ArchiveOrganizationAsync` against Polar before returning the error.
- **Proposed task:** `TASK-V20-016 — Onboarding compensation on partial-step failure`

---

## 2. Performance gaps

### 2.1 Advanced reporting materializes the whole orders table client-side

**File:** `src/PolarSharp.Reporting.EntityFrameworkCore/EfAdvancedReportingClient.cs:36-40, 70-75, 140-145, 171-176, 210-214, 269-274, 299-303, 343-347, 369-373, 402-410`

Nearly every method does:

```csharp
var orders = (await _db.Orders.AsNoTracking()
    .Select(o => new { ... })
    .ToListAsync(ct).ConfigureAwait(false))
    .Where(o => o.CreatedAt >= request.From && o.CreatedAt < request.To)
    .ToList();
```

The comment on lines 35-38 explains this is "because SQLite can't translate DateTimeOffset range filters when combined with the global tenant filter expression". That's true for SQLite, but the **same code runs against SQL Server and PostgreSQL** — and EF Core happily translates `DateTimeOffset >=` there. Forcing materialization-then-filter on production providers loads the entire tenant's order set into memory for every report. At 100k orders per tenant, that is hundreds of MB per report call.

- **Severity:** P1 once tenants cross ~10k orders. Today's tests run with seed-scale data and miss this.
- **Remediation:** Branch on the provider — keep the materialize path for SQLite only; use server-side filters for SQL Server / Npgsql. EF Core exposes `_db.Database.IsSqlite()` etc. Even better, change the global tenant filter to use captured-closure parameterization so SQLite can translate range filters too.
- **Proposed task:** `TASK-V20-020 — Provider-aware reporting query translation`

### 2.2 Drilldown reads have classic N+1 risk

**File:** `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore/Reading/EfPolarCatalogReader.cs:40-89`

`GetProductLocalizedAsync` issues four sequential awaits — product, variants, category-ids, translations. For a single-product call that's fine. But there is no batched `GetProductsLocalizedAsync(IEnumerable<ProductId>)` overload, so a host listing 50 products on a page will fire 200 round trips.

- **Severity:** P2
- **Remediation:** Add bulk overloads (`GetProductsLocalizedAsync`, `GetCategoriesLocalizedAsync`) that hoist the per-item awaits into single SELECTs with `WHERE Id IN (...)`.
- **Proposed task:** `TASK-V20-021 — Bulk reader overloads on IPolarCatalogReader`

### 2.3 Some report tables lack composite indexes for hot filter combinations

**File:** `src/PolarSharp.Reporting.EntityFrameworkCore/Configurations/SnapshotConfigurations.cs`, `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore/Configurations/CatalogConfigurations.cs`

The catalog has good coverage. Spot checks:
- `LocalProductEntity`: indexes on `(TenantId, MasterName)`, `(TenantId, PolarProductId)`, `(TenantId, IsFakeData)` — good.
- `ReportOrderEntity`: `(TenantId, CustomerId, CreatedAt)`, `(TenantId, PolarOrderId)` unique, `(TenantId, CreatedAt)` — good.
- **Missing:** no index on `ReportOrderEntity.Status`. The cross-tenant order-volume report filters on Status. No index on `ReportOrderEntity.Currency` (the per-currency report scans).
- **Missing:** `ReportSubscriptionEntity.CanceledAt` (used in churn cohort scan, `EfAdvancedReportingClient.cs:140-144`).
- **Missing:** `AdminAuditLogEntry.Action` and `(TenantId, ActorId)` indexes for "who changed this" queries.

- **Severity:** P2
- **Remediation:** Add the indexes; rerun query plan profiling on the SQL providers.
- **Proposed task:** `TASK-V20-022 — Index audit on Reporting + audit-log tables`

### 2.4 Page-size cap is inconsistent

**File:** `src/PolarSharp.Reporting.EntityFrameworkCore/EfPolarReportingClient.cs:27`, `EfAdvancedReportingClient.cs:68, 111, 297, 341`

- `IPolarReportingClient` paged methods cap at **500**.
- `IAdvancedReportingClient.GetTopProductsAsync` / `GetTopCustomersAsync` cap at **100**.
- Cross-tenant reports cap at **500**.
- `IPolarCatalogReader` has no paged methods at all — callers list-all.

The cap is *enforced* via `Math.Clamp` but the inconsistency means a host that gets used to 500 will hit a silent clamp to 100 on a different endpoint. There is also no enforcement of `request.PageSize >= 0` (Clamp lower bound of 1 is fine but a caller passing negative `Page` just generates a SQL `OFFSET -5` and EF errors out).

- **Severity:** P3
- **Remediation:** Centralize page-size into a single `PolarPaging` static (`Max = 500`, `Default = 50`), validate `Page >= 0`, and use everywhere.
- **Proposed task:** `TASK-V20-023 — Uniform paging policy`

### 2.5 DbContext pooling is not used; connection-pool sizing is unconfigured

`grep -rn "AddDbContextPool" src` returns zero hits. Every request allocates a fresh `DbContext`. For ASP.NET Core hosts with > 1k RPS this is a meaningful allocation cost. Likewise, `MaxConnectionsPerServer = 100` is hardcoded in `MultiTenantPolarClientFactory.cs:77` with no config override — this is the wrong knob for a 200-tenant host (effective ceiling = 100 × 200 = 20k sockets).

- **Severity:** P2
- **Remediation:** Switch internal `AddDbContext` to `AddDbContextPool` in the EF Core provider packages (configurable pool size); expose `MaxConnectionsPerServer` on `PolarMultiTenantOptions`.
- **Proposed task:** `TASK-V20-024 — DbContextPool + tunable HTTP connection ceiling`

### 2.6 Translation cache is per-entity, not per-tenant prefix-scanned

**File:** `src/PolarSharp.EcommerceStoreManagement/Translation/MemoryPolarCatalogTranslationCache.cs`

The cache is keyed by `(tenantId, entityType, entityId)`. Invalidating "all translations for tenant X" on language-pack upload would need a scan. `IMemoryCache` doesn't expose that, so the current code can only invalidate entries whose keys it already knows.

- **Severity:** P3
- **Remediation:** Add a per-tenant generation counter (incremented on tenant-scope translation rebuilds) and embed it in the cache key. Old keys age out naturally.
- **Proposed task:** `TASK-V20-025 — Tenant-prefix invalidation for translation cache`

---

## 3. Security hardening

### 3.1 Webhook secret rotation is not formally supported

**File:** `src/PolarSharp.Webhooks/PolarWebhookOptions.cs:46-49, 217-228`

The options expose both `Secret` (single) and `Secrets` (multiple) and the verifier walks the list — so dual-secret rotation works mechanically. But:
- There is no documented rotation procedure.
- There is no rotation timer or expiry on individual secrets — the operator must hand-edit appsettings.
- No metric counts "verifications by which secret index succeeded" — operators can't tell when it's safe to remove the old secret.

- **Severity:** P2
- **Remediation:** Document the rotation runbook; add a `polar.webhooks.secret_used_index` metric with the index of the matching secret; deprecate `Secret` (single) in favor of `Secrets[]` only.
- **Proposed task:** `TASK-V20-030 — Webhook secret rotation runbook + observability`

### 3.2 The promised RLS "layer 2 of 5" defense in depth does NOT exist in migrations

**File comment claim:** `src/PolarSharp.MultiTenant.EntityFrameworkCore/TenantAwareDbContextBase.cs:28-32` — "SQL Server and PostgreSQL providers additionally enforce Row-Level Security policies at the database layer (layer 2)"

**Reality:** `grep -rn "CREATE POLICY\|ENABLE ROW LEVEL\|sp_set_session_context" src` returns ZERO matches. The migrations under `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL/Migrations/20260513064050_Initial.cs` (420 lines) contain no `migrationBuilder.Sql` calls for RLS. No `sp_set_session_context` plumbing in SQL Server. No `app.tenant_id` GUC setting in PostgreSQL.

The documentation is **promising a security control that is not implemented**. The single layer of defense is the EF Core query filter, which means a raw-SQL access path or a forgotten `IgnoreQueryFilters()` is a full cross-tenant leak.

- **Severity:** P1 — this is a security claim mismatch. Either implement it or remove the documentation.
- **Remediation:** Either (a) add real RLS migrations to the SQL Server and PostgreSQL packages plus a per-request `SET LOCAL app.tenant_id` interceptor; or (b) remove the "layer 2" claim from the docs and the class-level remarks.
- **Proposed task:** `TASK-V20-031 — Implement RLS policies OR remove the layer-2 claim`

### 3.3 The "AuditLogSaveChangesInterceptor" referenced in docs does not exist

**File comment claim:** `src/PolarSharp.EcommerceStoreManagement/AdminAuditLogEntry.cs:7-9` — "Filled by `AuditLogSaveChangesInterceptor` on every `SaveChanges`"

**Reality:** `grep -rn "SaveChangesInterceptor\|AuditLogSaveChangesInterceptor" src` returns only the comment itself. No interceptor type exists. Audit log entries are inserted by hand in service code (refunds, business-profile saves) — meaning **any code path that mutates a tenant-owned entity without explicitly writing an audit row leaves no trace**.

- **Severity:** P1 — audit completeness is a compliance and forensics requirement.
- **Remediation:** Implement `AuditLogSaveChangesInterceptor` that walks `ChangeTracker.Entries()` on each `SaveChanges`, computes the diff against `BeforeValues`, and writes an `AdminAuditLogEntry`. Register on every PolarSharp DbContext.
- **Proposed task:** `TASK-V20-032 — Real audit-log SaveChanges interceptor`

### 3.4 Log injection risk on tenant slug

The current logging passes tenant ids as `{TenantId}` structured properties — good. But the `InvalidOperationException` messages in `MultiTenantPolarClientFactory.cs:49-57` interpolate `tenantInfo.Identifier` directly into the message string. If a tenant slug ever contained a newline or ANSI escape, that ends up in error responses and aggregated logs.

- **Severity:** P3 (depends on what aggregator picks it up)
- **Remediation:** Validate slug format at tenant-creation time (`^[a-z0-9-]{1,64}$`). Reject anything else at the registration boundary.
- **Proposed task:** `TASK-V20-033 — Strict tenant slug validation at boundary`

### 3.5 JSON depth budget is set but size budget is not

**File:** `src/PolarSharp/Serialization/PolarJsonContext.cs:28` — `MaxDepth = 32` is set. Good. There is no equivalent `MaxBytes`, no Kestrel `MaxRequestBodySize` override for webhook endpoints. A malicious or buggy webhook source could send a multi-GB body and exhaust memory before the dedup store ever sees it.

- **Severity:** P2
- **Remediation:** Add `WebhookEndpointOptions.MaxBodyBytes` (default 256KB; Polar's real payloads are well under this) and reject early via a middleware/filter.
- **Proposed task:** `TASK-V20-034 — Body-size budget on webhook endpoint`

### 3.6 No CSRF protection on onboarding-wizard endpoints

**File:** `src/PolarSharp.Onboarding/Wizard/OnboardingWizard.cs:90-117`

The wizard exposes step-submit methods that take POST-shaped data. The wizard itself doesn't expose endpoints (the host wires them up), so this is technically a *host* responsibility — but the README doesn't say so, and PolarSharp's onboarding template doesn't show `[ValidateAntiForgeryToken]` on the example controllers.

- **Severity:** P2 (depends on host implementation)
- **Remediation:** Document the CSRF requirement explicitly in `PolarSharp.Onboarding/README.md`; add a sample controller in the templates that demonstrates `[ValidateAntiForgeryToken]`.
- **Proposed task:** `TASK-V20-035 — CSRF guidance in onboarding docs + templates`

---

## 4. Observability gaps

### 4.1 OpenTelemetry trace coverage is HTTP-boundary only — domain operations are invisible

**File:** `src/PolarSharp/Telemetry/PolarActivitySource.cs`

`StartActivity` is called from the Polar HTTP layer. Domain-side operations — `IPolarCatalogPublisher.PublishAsync`, `IPolarCatalogReader.GetProductLocalizedAsync`, `IRefundService.IssueFullRefundAsync`, `ReportSnapshotService.RunOnceForTenantAsync` — have no spans. A trace from "merchant clicks publish" through "X products published" is broken.

- **Severity:** P2
- **Remediation:** Wrap every public method on the published interfaces in `using var _ = activitySource.StartActivity("polarsharp.publisher", "publish_all", tenantId)`. Add latency tags.
- **Proposed task:** `TASK-V20-040 — Domain-level trace spans on every public interface`

### 4.2 Per-tenant metric cardinality can explode

**File:** `src/PolarSharp/Telemetry/PolarMeter.cs:122-145`

`RecordRequest` and `RecordError` emit `polar.tenant_id` as a tag on every counter. With 1000 tenants × 5 resources × 4 operations × ~10 status codes, that is 200k distinct time series — *per metric*. Prometheus and Azure Monitor charge by series count.

- **Severity:** P2 at scale
- **Remediation:** Add `PolarMeterOptions.PerTenantLabelMode` with `Always | OnlyForLowCardinality | Aggregated`. Document the trade-off.
- **Proposed task:** `TASK-V20-041 — Bounded-cardinality tenant labels in metrics`

### 4.3 Health checks don't distinguish liveness from readiness

`grep -rn "Predicate.*ready\|tags.*ready\|tags.*live" src` returns nothing. Every health check is registered with miscellaneous tags but the SDK doesn't expose a `Predicate = check => check.Tags.Contains("ready")` filter. K8s probes can't tell startup-stalled from runtime-degraded.

- **Severity:** P2
- **Remediation:** Standardize on tags `live | ready | startup`. Document the recommended `MapHealthChecks("/health/live", new() { Predicate = c => c.Tags.Contains("live") })` recipe.
- **Proposed task:** `TASK-V20-042 — Readiness/liveness/startup tag conventions`

### 4.4 No health check covers "webhook signing keys still recognized by Polar"

The DB-ping health checks confirm the database is reachable. Nothing confirms the *configured* webhook secret matches what Polar would actually sign with. A rotation in Polar that wasn't mirrored to the host is invisible until a real event is rejected.

- **Severity:** P2
- **Remediation:** Add a `polar-webhook-secret` health check that calls Polar's `GET /v1/webhooks/{id}` and compares the secret prefix. Mark Degraded (not Unhealthy) when they mismatch — the old secret may still be valid during rotation grace.
- **Proposed task:** `TASK-V20-043 — Webhook-secret-drift health check`

### 4.5 Structured-log property names are not centralized

Logs use `{TenantId}`, `{Tenant}`, `{TenantName}` inconsistently across files. (`MultiTenantPolarClientFactory.cs:65-67` uses both `{TenantId}` and `{TenantName}` in the same call; some webhook logs use `{webhookId}` lowercase.)

- **Severity:** P3
- **Remediation:** Define `PolarLogProperties` constants and use them everywhere; lint via an analyzer or a unit test that greps the source.
- **Proposed task:** `TASK-V20-044 — Canonical structured-log property names`

---

## 5. Operational gaps

### 5.1 No documented zero-downtime migration story

The migrations under `src/PolarSharp.*.PostgreSQL/Migrations/` use Add/Drop column raw operations. Nothing here is destructive yet (v1.3.0 is the first SchemaStable release), but there is no documented "expand → backfill → contract" guidance for v2.0 onwards, and no migration scaffold that produces split migrations.

- **Severity:** P2 (will become P1 the first time a v1 customer needs to upgrade to v2 without a maintenance window)
- **Remediation:** Add an `operations/migrations.md` runbook; add an FxCop / analyzer rule that flags `DropColumn` in a non-final-migration without explicit ack.
- **Proposed task:** `TASK-V20-050 — Zero-downtime migration playbook`

### 5.2 Tenant deletion has no defined contract

`grep -rn "DeleteTenantAsync\|RemoveTenantAsync\|TenantDeleted" src` returns nothing relevant. What happens when an operator deletes a tenant?
- The tenant store row goes away.
- The per-tenant SQLite file (Sqlite provider) is orphaned on disk.
- Audit log entries for that tenant remain (good — compliance).
- The per-tenant `PolarClient` entry in `MultiTenantPolarClientFactory._entries` is never evicted (memory leak; line 138-139 only clears on full dispose).
- The per-tenant Polly circuit-breaker state retains forever.

- **Severity:** P2
- **Remediation:** Define `IPolarTenantLifecycle.RetireTenantAsync(tenantId, ct)` with documented side effects; emit a `TenantRetired` event; have `MultiTenantPolarClientFactory` subscribe and evict.
- **Proposed task:** `TASK-V20-051 — Tenant-retirement contract and cleanup hooks`

### 5.3 Per-tenant SQLite files have no documented backup / restore tooling

The Sqlite provider mode (per-tenant DB files) is convenient for small SaaS but the docs don't say:
- Where the files live by default.
- How to back them up atomically (you need `VACUUM INTO` or the SQLite Online Backup API; `cp` is unsafe under write load).
- How to migrate them in bulk when schema changes.

- **Severity:** P3
- **Remediation:** Add an `operations/sqlite-backup.md` doc; ship a CLI sample that uses `Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase`.
- **Proposed task:** `TASK-V20-052 — SQLite-tenant backup tooling`

### 5.4 Schema-version skew between snapshot service and report consumers is not detected

If the snapshot service (writer) advances to a new schema version while the report consumer (reader) is still on the old release, missing columns or new enum values are silently misread. `ReportSnapshotCheckpointConfig` does not include a `SchemaVersion` column.

- **Severity:** P3
- **Remediation:** Add `SchemaVersion` to checkpoint rows; have the reader log Warning when reading rows from a higher version.
- **Proposed task:** `TASK-V20-053 — Schema-version checkpoint on snapshot rows`

---

## 6. Multi-tenancy edge cases

### 6.1 Tenant cache coherency in multi-instance hosts (already tracked as OQ-14)

**File:** `src/PolarSharp.MultiTenant.EntityFrameworkCore/MemoryPolarTenantCache.cs`

In-process cache only. Multi-instance hosts see a stale tenant config until the cache TTL elapses. Acknowledged in the plan docs.

- **Severity:** P2
- **Remediation (already planned):** The `Distributed` cache provider option exists — finish wiring it, ship a Redis sample, document the trade-off.
- **Proposed task:** `TASK-V20-060 — Distributed tenant cache via IDistributedCache`

### 6.2 The cross-tenant constant-folding bug — is it really gone?

**File:** `src/PolarSharp.MultiTenant.EntityFrameworkCore/TenantAwareDbContextBase.cs:126-155`

The fix (using `Expression.Field(Expression.Constant(this))` instead of `Expression.Constant(_currentTenantId)`) is correct in concept. The risk is real-world: this only works if **every** PolarSharp DbContext type that derives from `TenantAwareDbContextBase` registers as **Scoped** (not Singleton or DbContextPool with shared state across requests). A future `AddDbContextPool<PolarCatalogDbContext>()` opt-in would *reintroduce the leak* because pooled contexts reuse the closure across tenants. The fix doc on lines 126-138 is essential — but there is no analyzer enforcing it.

- **Severity:** P2 — fragile invariant, not currently broken
- **Remediation:** Add a Roslyn analyzer (or at minimum a startup check) that throws on registration if a `TenantAwareDbContextBase`-derived type is registered via `AddDbContextPool`.
- **Proposed task:** `TASK-V20-061 — Startup guard against pooled tenant-aware DbContexts`

### 6.3 No per-tenant resource quotas

A merchant calling `IPolarDataSeeder.SeedFullCatalogAsync(SeedScale.Stress)` generates 50k+ products. On a shared SQL Server, that one tenant's transaction can starve every other tenant for the duration. No `IFakeDataPolicy.MaxRowsPerTenant`. No `IRateLimiter` around seeding.

- **Severity:** P2
- **Remediation:** Add `PolarDataSeedingOptions.MaxFakeRowsPerTenant` (default 5000); enforce in `PolarDataSeeder`. Wrap seed-write operations in a per-tenant concurrency limiter.
- **Proposed task:** `TASK-V20-062 — Per-tenant resource quotas on seeding and bulk ops`

### 6.4 Noisy-neighbor protection at the HTTP layer is single-tier

`TenantResilienceDelegatingHandler` isolates tenants from each other on the **outbound** Polar API path. On the **inbound** ASP.NET Core path, every tenant shares the same Kestrel thread pool. A single tenant's webhook flood is rate-limited by the fixed-window limiter — but that limiter is **per-endpoint, not per-tenant**. One noisy tenant burning through the bucket starves the others.

- **File:** `src/PolarSharp.Webhooks/Extensions/WebhookBuilderExtensions.cs:608-613`
- **Severity:** P2 at scale
- **Remediation:** Switch the rate limiter to `PartitionedRateLimiter` keyed on tenant id.
- **Proposed task:** `TASK-V20-063 — Per-tenant webhook ingress rate limiter`

---

## 7. Public API surface stability

### 7.1 Unsealed public classes that should be sealed

The codebase is mostly sealed (68 sealed types under `EcommerceStoreManagement` alone). The remaining unsealed public classes:

- `PolarReportingDbContext` (`Reporting.EntityFrameworkCore/PolarReportingDbContext.cs:12`) — host customers will subclass this to add bespoke DbSets. **Keep unsealed.** Document the contract.
- `PolarTenantDbContext` — same. Keep unsealed.
- `PolarUserDbContext` — same; ASP.NET Core Identity requires subclassing. Keep unsealed.
- `PolarApplicationUser`, `PolarApplicationRole` — Identity types must be subclassable. Keep unsealed.
- `PolarMultiTenantOptions`, `PolarOptions`, `PolarResilienceOptions`, `PolarConnectionOptions`, `PolarLoggingOptions`, `ClaimStrategyOptions`, `HostnameStrategyOptions`, `HeaderStrategyOptions`, `RouteStrategyOptions`, `PolarReconciliationOptions`, `PolarToastOptions`, `PolarToastEventConfig`, `PolarWebhookOptions`, `PlatformAuditLogEntry`, `PolarUserTenantMembership` — these are POCO option types or entity types. Should be sealed before v2.0 freezes the contract — subclassing breaks JSON-binding, EF mapping, and value semantics.

- **Severity:** P2 (subclassing in the wild becomes a v2.0 BC break)
- **Proposed task:** `TASK-V20-070 — Seal POCO option types and entity types before v2.0`

### 7.2 Surface-level inconsistencies that should be `[Obsolete]` before v2.0

- `PolarWebhookOptions.Secret` (single) vs `PolarWebhookOptions.Secrets[]` — keep one, deprecate the other (the `Secrets[]` array is the rotation-friendly one; `Secret` should be `[Obsolete("Use Secrets[]")]`).
- `IPolarReportingClient.GetSubscriptionsAsync` returns hardcoded zeros (`EfPolarReportingClient.cs:71-80`). Document the stub status with `[Obsolete("Stub returns zeros until TASK-V20-005 lands")]` or remove from the interface until implemented.

- **Severity:** P3
- **Proposed task:** `TASK-V20-071 — Pre-v2 Obsolete annotations on deprecated/stub APIs`

### 7.3 Internal types unintentionally exposed via inheritance

`AdminAuditLogEntry` is public (`src/PolarSharp.EcommerceStoreManagement/AdminAuditLogEntry.cs:22`), with `BeforeValues`/`AfterValues` as `JsonNode?`. Hosts can now write their own audit rows with arbitrary JSON content — which is a feature, but the public class has no validation. A host writing 10MB JSON nodes will hit EF Core's translation to `nvarchar(max)` and the row goes through.

- **Severity:** P3
- **Remediation:** Either seal `AdminAuditLogEntry` and add validation, or document the max-size contract explicitly.
- **Proposed task:** `TASK-V20-072 — AuditLogEntry size contract`

---

## 8. Documentation / operability for ops teams

### 8.1 Missing runbooks

The DocFX site has Implementation Narratives for users. It does not have:
- A "webhook deliveries stalled" runbook (queue depth metric → DLQ inspection → replay).
- A "RLS bypass alarm" runbook (currently moot — no RLS exists, see 3.2 — but will matter once it does).
- A "migration failed mid-deploy" runbook (which migration is the new schema-snapshot pinned to? what's safe to roll back?).
- A "tenant onboarding stalled at webhook-registration" runbook (matches 1.7 above).
- A "circuit breaker open for tenant X" runbook (where to find the metric, how to manually half-open).

- **Severity:** P2
- **Proposed task:** `TASK-V20-080 — Operator runbooks under docs/runbooks/*.md`

### 8.2 No CHANGELOG vs release-notes split

`CHANGELOG.md` currently fits both audiences. As packages multiply, the engineer-facing detail makes the merchant-facing changelog opaque. Split into:
- `CHANGELOG.md` — engineer-facing, every PR.
- `docs/release-notes/v1.3.0.md` — merchant-facing, "what's new for you".

- **Severity:** P3
- **Proposed task:** `TASK-V20-081 — Split CHANGELOG and release notes`

### 8.3 No supported-version matrix

What versions of .NET, EF Core, Finbuckle, Telerik UI for Blazor, SQL Server, PostgreSQL are tested? The Directory.Packages.props pins versions; no published matrix says "v1.3.0 supports .NET 10 only; PostgreSQL 14+; SQL Server 2022+".

- **Severity:** P3
- **Proposed task:** `TASK-V20-082 — Supported-version matrix in docs/articles/compatibility.md`

---

## Top 10 priorities for v2.0

Ranked by combined severity × scaling risk × cost-to-fix:

1. **TASK-V20-031** — Implement RLS in PostgreSQL/SQL Server migrations, OR remove the "layer 2 defense" claim from documentation. *Security-posture mismatch.*
2. **TASK-V20-032** — Implement `AuditLogSaveChangesInterceptor`. *Compliance gap; doc claim doesn't match reality.*
3. **TASK-V20-020** — Provider-aware reporting query translation. *Materialize-then-filter pattern is a P1 perf cliff at scale.*
4. **TASK-V20-011** — Outbox for catalog publish state mutations. *Avoids local/Polar drift once HTTP wiring goes live.*
5. **TASK-V20-013** — Webhook dead-letter store and replay UI. *Today's failures are silently dropped.*
6. **TASK-V20-012** — Plan-time variant expansion. *Multi-variant publish is currently a single opaque call.*
7. **TASK-V20-061** — Startup guard against pooled tenant-aware DbContexts. *Closes a fragile-invariant footgun.*
8. **TASK-V20-040** — Domain-level trace spans on every public interface. *Operability for non-HTTP failures.*
9. **TASK-V20-030** — Webhook secret rotation runbook + observability. *Rotation is mechanically supported but operationally undefined.*
10. **TASK-V20-062** — Per-tenant resource quotas on seeding and bulk operations. *Noisy-neighbor protection for shared deployments.*

---

## What is already excellent

Don't lose sight of how much of this is already very good. PolarSharp v1.3.0 ships a number of things that most "extending a SaaS API" libraries never get right:

- **Per-tenant bulkhead isolation done properly** — separate `SocketsHttpHandler`, separate Polly pipeline, separate auth header, race-free initialization via `LazyConcurrentDictionary`. Most SDKs share one `HttpClient` across tenants and call it a day. (`MultiTenantPolarClientFactory.cs`)
- **The Result<T, E> monad is consistent across the entire API surface** — every async method returns `Task<Result<X, Y>>`. No mixing of exceptions and result values. Zoran would approve.
- **AOT-safe, source-generated JSON with depth budget set** — `PolarJsonContext` with `MaxDepth = 32` and explicit context-mode serialization. AOT and trimming flags are wired up at the project level.
- **The cross-tenant constant-folding fix is documented in the code itself** — `TenantAwareDbContextBase.ApplyFilter` has a 13-line comment block explaining exactly why `Expression.Field(Expression.Constant(this))` is required. That kind of contextual documentation is rare and valuable.
- **Idempotency keys auto-attached to mutating methods** — even if caller-supplied keys would be better (see 1.1), the default is correct.
- **Onboarding wizard encrypts translation API keys at rest via ASP.NET Core Data Protection** before persistence (`OnboardingWizard.cs:104-110`). Real DP, not hand-rolled crypto.
- **DryRun mode on the catalog publisher** — preview-without-side-effects is a first-class feature, not an afterthought.
- **Audit log schema is well-designed even though the interceptor is missing** — `BeforeValues`/`AfterValues` as JSON, `ChangedFields` as a comma-separated string, `CrossTenantJustification` for AppMasterAdmin operations. The plumbing is there; only the writer is missing.
- **Strong-named, deterministic, source-linked NuGet packages** — `Deterministic=true`, `EmbedUntrackedSources`, `PublishRepositoryUrl`, `Microsoft.SourceLink.GitHub`. Every shipped DLL traces back to a git SHA.
- **CS1591 is build-error** — every public symbol has XML docs or the build fails. Most projects give up on this within a year.
- **Tests actually exercise the behaviour** — 379 tests across 8 projects covering cross-tenant isolation specifically, with JSON snapshot tests for error shapes. The coverage is real, not just lines.

The work below is making a solid foundation harder to break under load. None of it suggests the foundation itself is wrong.
