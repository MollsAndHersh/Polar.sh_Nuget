---
task: V20-020 Phase A — memory-leak audit
date: 2026-05-14
auditor: Claude (Opus 4.7, 1M context)
scope: src/ across all 31 PolarSharp packages; testapp call-site review
methodology: see DESIGN-V20-020-MEMORY-LEAK-AUDIT.md (8 leak patterns, manual + grep sweeps)
---

# Phase A — Memory Audit Findings

## Triggering question

Will any PolarSharp triggering / background mechanism leak resources if a host
application is forcibly shut down (Ctrl-C, container kill, fatal exception),
or accumulate unbounded state across a long-running process?

## Method

1. Manual review of every `IHostedService`, `BackgroundService`, and class
   holding raw `IDisposable` fields (Timer / SemaphoreSlim / HttpClient /
   CancellationTokenSource / Channel / DbContext).
2. Grep sweep for the 8 leak patterns enumerated in
   `DESIGN-V20-020-MEMORY-LEAK-AUDIT.md`.
3. Inspection of call sites for any class that newly creates raw HttpClient.

## Findings summary

| Severity | Class | Disposition |
|---|---|---|
| 🚨 **HIGH — fixed in this audit** | `PolarCustomerPortalClient` | Fresh `HttpClient` per instance; class was not `IDisposable`. **FIX:** implemented `IDisposable`, dispose `HttpClient` in `Dispose()`. Updated XML doc + test-app call sites to use `using`. |
| ⚠️ FUTURE | `MultiTenantPolarClientFactory` | No per-tenant eviction API — deleted tenants leak one HttpClient + handler chain for the rest of the host's lifetime. Not a shutdown leak; a long-running concern. **Tracked for v2.x.** |
| ✅ Already fixed (prior session) | `PerTenantSnapshotOrchestrator.StopPeriodicAsync` | Now does `_tenants.TryRemove` + disposes `state.Semaphore`. Verified via `PerTenantSnapshotOrchestratorTests.StopPeriodic_removes_tenant_state_from_internal_dict_no_leak`. |

All other audited classes are clean.

## Per-class disposition

### Already-fixed (verified clean before audit)

- `PerTenantSnapshotOrchestrator` — Singleton; `StopPeriodicAsync` removes
  TenantState from dict and disposes its `SemaphoreSlim`; idle-timeout uses
  the same path; bounded `Channel<SnapshotCompletedEvent>(100, DropOldest)`
  cannot grow unbounded.

### Caches (clean)

- `MemoryPolarTenantCache` — wraps DI-owned `IMemoryCache`; TTL-based
  eviction; no raw resources.
- `MemoryPolarCatalogTranslationCache` — same shape.

### Background services (clean)

- `PolarWebhookBackgroundService` — `BackgroundService` with proper drain
  semantics on `StopAsync` (`drainCts` is `using`-scoped).
- `PolarWebhookBackgroundQueue` — bounded `Channel`; producer/consumer
  back-pressure handled.
- `FakeDataSyncService` — `BackgroundService` with `await foreach` over the
  channel + `using var scope` per event.
- `OnboardingSessionExpirationCleaner` — `using var timer = new PeriodicTimer`
  + `await using var scope` per tick. Timer disposed when `ExecuteAsync`
  unwinds.
- `WebhookInMemoryDedupService` — `_pruneTimer` allocated in `StartAsync`,
  awaited+disposed in `StopAsync`; `_seen` dict has bounded `MaxEntries`
  with oldest-first eviction.

### Lifetime / channel managers (clean)

- `PolarToastChannelLifetime` — disposes both `_meter` (Meter) and
  `_stoppingRegistration` (CancellationToken registration) in `StopAsync`.
- `InventoryUpdater` — Scoped, EF DbContext-bound, no raw resources.
- `ChannelInventoryEventNotifier` — wraps an injected `ChannelWriter`; no
  ownership.

### Startup-only IHostedServices (clean — `StartAsync` runs once, `StopAsync` is no-op)

These run their work once at process start and hold no resources:

- `AppMasterAdminBootstrapper`
- `TenantAdminInvariantValidator`
- `AppSettingsSeeder`
- `RoleSeeder`
- `PolarMigrationRunner`
- `PolarStartupService`
- `PolarWarmupService`

## Fix applied this audit

### `src/PolarSharp/CustomerPortal/PolarCustomerPortalClient.cs`

**Before:** class was `public sealed class` (no `IDisposable`); constructor
allocated `var httpClient = new HttpClient()` as a local that escaped only
into the `HttpClientRequestAdapter` and was forever unreachable for disposal.

**After:** class declares `: IDisposable`; constructor stores `_httpClient`
field; `Dispose()` disposes it (idempotent via `_disposed` guard). Updated
the XML `<example>` and the `PolarClient.CreateCustomerPortalClient` `<remarks>`
to call out the disposal requirement. Updated the two test-app endpoints
(`/test/customer-portal/orders`, `/test/customer-portal/subscriptions`) to use
`using var`.

**Impact for hosts:** any application calling
`polar.CreateCustomerPortalClient(token)` without disposal was leaking one
`HttpClient` + `SocketsHttpHandler` + connection pool per call. For SaaS hosts
that mint a fresh portal client per customer login, that's one leak per
customer login until the process restarts. After the fix, the recommended
pattern is `using var portal = polar.CreateCustomerPortalClient(token);`.

## Future work (not blocking v2.0)

### `MultiTenantPolarClientFactory` — long-running tenant churn

The factory caches one `HttpClient` + `SocketsHttpHandler` + Polly pipeline
per tenant in `_entries` (`LazyConcurrentDictionary`). On host shutdown,
`DisposeAsync` snapshots and disposes everything correctly — no shutdown
leak.

Long-running concern: there is no public `RemoveTenant(tenantId)` /
`EvictTenant(tenantId)` API. If the host deletes a tenant at runtime (or a
tenant is offboarded), its entry stays in `_entries` for the rest of the
host's lifetime — one HttpClient + handler chain per deleted tenant. For
SaaS deployments with ongoing tenant churn, this is a slow leak.

**Suggested fix (v2.x):** add `void EvictTenant(string tenantId)` to
`IMultiTenantPolarClientFactory` that removes the entry from `_entries` and
disposes its `HttpClient`. Wire onboarding-deletion paths to call it.

## Verification

- `dotnet build src/PolarSharp/PolarSharp.csproj` — 0 warnings, 0 errors.
- Existing `PolarSharp.Tests` suite covers the customer portal — re-run as
  part of the post-audit validation step.

## Phase B — recommended next steps

1. Run the full test suite to confirm no regressions from the
   `PolarCustomerPortalClient` interface change.
2. Consider adding a unit test that exercises
   `using var portal = ...; var p2 = portal; portal.Dispose();` and asserts
   `Dispose` is idempotent.
3. Schedule the `MultiTenantPolarClientFactory` eviction work for v2.x.
