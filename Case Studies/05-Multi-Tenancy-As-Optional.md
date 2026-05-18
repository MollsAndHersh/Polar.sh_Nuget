---
title: "Multi-Tenancy as Optional for Library Authors"
short_title: "Multi-Tenancy as Optional"
case_study_id: "05"
author:
  name: "Mark Chipman"
  organization: "Molls and Hersh, LLC"
date_published: 2026-05-15
date_modified: 2026-05-18
status: stable
license: "© Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution."
reference_implementation: "PolarSharp.PrepaidWallets v1.3 + PolarSharp.EcommerceStorefronts v1.4 (planned)"
keywords:
  - multi-tenancy
  - single-tenant deployment
  - mode-agnostic abstractions
  - library design
  - tenant scope
  - optional multi-tenancy
  - audience scope
  - Finbuckle.MultiTenant
  - EF Core query filters
  - tenant isolation
  - dual-mode libraries
related_case_studies:
  - "01-Lift-And-Shift-Architecture"
  - "02-Event-Sourced-Wallet-With-Economic-Modeling"
related_patterns:
  - "Null Object Pattern"
  - "Optional / Maybe / Option types"
  - "Feature Flags"
  - "Configuration as Code"
ecosystems:
  primary: ".NET / NuGet"
  generalizes_to: ["Finbuckle.MultiTenant", "ASP.NET Core Identity", "EF Core global query filters", "Hot Chocolate GraphQL schema variants", "single-tenant SaaS / multi-tenant SaaS"]
---
<!--
JSON-LD structured data (invisible on GitHub render; consumed by web crawlers + ontology-aware AI agents):
{
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "Multi-Tenancy as Optional for Library Authors",
  "alternativeHeadline": "Multi-Tenancy as Optional",
  "author": {
    "@type": "Person",
    "name": "Mark Chipman",
    "affiliation": {
      "@type": "Organization",
      "name": "Molls and Hersh, LLC"
    }
  },
  "datePublished": "2026-05-15",
  "dateModified": "2026-05-18",
  "inLanguage": "en",
  "keywords": "multi-tenancy, single-tenant deployment, mode-agnostic abstractions, library design, tenant scope, optional multi-tenancy, audience scope, Finbuckle.MultiTenant, EF Core query filters, tenant isolation, dual-mode libraries",
  "about": [
    "optional multi-tenancy",
    "mode-agnostic library design",
    "single-tenant fallback",
    "audience-tier collapse",
    "query-filter conditionality",
    "schema-mode auto-detection",
    "dual-mode deployments"
  ],
  "isPartOf": {
    "@type": "CreativeWorkSeries",
    "name": "PolarSharp Architectural Case Studies"
  },
  "license": "© Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution.",
  "proficiencyLevel": "Expert"
}
-->

# Case Study 05 — Multi-Tenancy as Optional for Library Authors

> **Author**: Mark Chipman — Molls and Hersh, LLC.
> **Date**: 2026-05-18
> **Status**: Stable. Reference implementation: PolarSharp.PrepaidWallets v1.3 + PolarSharp.EcommerceStorefronts v1.4 (planned).
> **License**: © Mark Chipman / Molls and Hersh, LLC. 2026. Educational use permitted with attribution.
> **Related files**: PolarSharp v1.3 plan, "Multi-tenancy is OPTIONAL" subsection in the PrepaidWallets section.

## TL;DR

A library that needs to serve both single-tenant and multi-tenant host applications should architect its abstractions to be MODE-AGNOSTIC from day one. The library defines an `IIdentityProvider`-style interface with both `CurrentUserId` AND an `Option<TenantId>` that returns `None` in single-tenant mode, plus an explicit `IsMultiTenantMode` flag. Audience-tier enums collapse meaningfully ("Tenant" tier resolves to "HostOperator" tier in single-tenant deployments). Query filters skip the tenant clause when context is None. Bridges adapt the library's mode-agnostic abstractions to the host's actual identity / authorization / tenancy infrastructure. The result: the SAME library CORE binary works in both deployment models without code forks, conditional compilation, or two separate NuGet packages — the only thing that differs per deployment is which bridge package the host installs.

## Historical context / inspiration / prior art

The .NET ecosystem has historically split multi-tenancy support across two camps:

- **MT-native libraries** (Finbuckle.MultiTenant, Microsoft's Orchard Core multi-tenancy, ASP.NET Core's `IMultiTenantContextAccessor` patterns) — designed for MT from the start; using them in single-tenant deployments requires registering null/identity tenant resolvers; feels heavy for single-tenant projects.
- **Single-tenant libraries** (most ASP.NET Identity, most CRUD samples) — assume one global context; adding MT requires either forking the library OR layering tenancy on top with a complex bridge.

Library authors typically pick ONE camp and the other half of potential users walks away. The exceptions are rare and notable:
- **Microsoft.Extensions.Identity.*** — single-tenant by default; MT requires significant external wiring (Finbuckle being the most popular).
- **Hangfire** — single-tenant by default; MT requires a queue-per-tenant convention rolled by the developer.
- **MediatR** — agnostic by design; works in both modes because it has no built-in tenancy concept at all.

The closest pattern I've seen explicitly DESIGNED for both modes is **EF Core's global query filters** — they support tenancy via a filter on a tenant-id column, AND they support being totally absent when the entity has no tenant column. But EF Core itself doesn't have a `IsMultiTenantMode` flag; it just lets you opt in to filters per-entity.

The contribution of this case study is articulating mode-agnosticism as a FIRST-CLASS LIBRARY DESIGN PRINCIPLE, with specific implementation patterns (the mode-agnostic identity provider, the audience-tier collapse, the schema/UI auto-adaptation in bridges) that library authors can adopt.

## Problem

A library author wants to ship a meaningful feature (wallets, ecommerce storefronts, billing, analytics, anything) that some hosts will deploy as multi-tenant SaaS and other hosts will deploy as single-tenant single-org. The library author must decide:

**Path A: ship two libraries.** `LibFoo.SingleTenant` and `LibFoo.MultiTenant`. Tenants pick the right one. Pros: each is simpler internally. Cons: doubled maintenance burden, shared bug fixes drift, documentation is twice as long, and upgrading from single-tenant to multi-tenant requires switching packages. Examples: many ASP.NET Identity samples that ship a separate `Foo.MultiTenant` package alongside `Foo`; the original Hangfire Pro multi-tenant tier.

**Path B: ship MT-only, force single-tenant hosts to use a null/identity tenant.** Pros: one binary. Cons: single-tenant hosts pay the MT overhead for no benefit; the abstraction feels heavy; some MT features (cross-tenant queries) don't even make sense in single-tenant land but their existence in the API surface is confusing. Examples: Finbuckle.MultiTenant itself (single-tenant hosts use the null tenant strategy); Orchard Core's multi-tenancy module.

**Path C: ship single-tenant-only, force MT hosts to wrap.** Pros: one binary; simple. Cons: MT hosts can't actually use the library without extensive wrapping; the wrapping is a recurring source of bugs and incompatibilities. Examples: most of Microsoft.Extensions.Identity (MT requires Finbuckle on top); Hangfire's open-source tier.

**Path D: ship a mode-agnostic library that works correctly in both deployments.** Pros: one binary; correctly tuned for each deployment; no wrapping or forking. Cons: requires more careful abstraction design up front; some library types (`Option<TenantId>` instead of `TenantId`) have slightly more friction than the single-tenant-natural shape. Examples: MediatR (mode-agnostic by design because it has no built-in tenancy concept at all); EF Core's global query filters (per-entity opt-in rather than library-wide). PolarSharp's wallet aims to extend this pattern to features that DO have native tenancy semantics — typically the hardest case.

Path D is the case study's recommendation when it's achievable. Most library features can be made mode-agnostic with modest design effort. The result is a library that captures both market segments AND lets single-tenant hosts upgrade to MT without switching packages.

## Forces / constraints

1. **Single-tenant hosts must not pay the MT performance cost.** No tenant-id column lookup, no tenant-context middleware, no IsAllowedCrossTenant accessor — none of it should fire in single-tenant deployments.
2. **MT hosts must get full MT semantics.** Tenant-scoped queries, audience-tier authorization, cross-tenant safeguards, etc.
3. **The library's abstractions must be the SAME interface for both modes.** Conditional compilation, separate base classes, or "if MT { do thing A } else { do thing B }" branching throughout the codebase is a code-smell explosion.
4. **The host's actual identity / tenancy infrastructure shouldn't constrain the library.** A single-tenant host using plain ASP.NET Core Identity should be able to use the library. An MT host using Finbuckle + KeyCloak + a custom claims transformer should also work.
5. **Audience tiers must collapse meaningfully.** A "SaaSAdmin / Tenant / Customer" 3-tier model in MT mode collapses to "HostOperator / Customer" in single-tenant mode. The library must do this automatically, not punt to the host.
6. **The UI / API surface must adapt automatically.** A GraphQL schema with tenant fields in MT mode shouldn't expose those tenant fields in single-tenant mode. A reporting client with cross-tenant queries in MT mode shouldn't expose them in single-tenant mode.
7. **Tests must run in both modes.** Both code paths must be exercised in CI; otherwise mode-agnosticism rots silently.

## The pattern

### Component 1: Mode-agnostic identity provider abstraction

```csharp
public interface ILibraryIdentityProvider
{
    Guid CurrentUserId { get; }              // who is performing this op; required
    Option<Guid> CurrentTenantId { get; }    // tenant scope; None in single-tenant deployments
    bool IsMultiTenantMode { get; }           // true ⇒ MT-aware host; false ⇒ single-tenant
}
```

`Option<T>` (or `Nullable<T>`) on `CurrentTenantId` is the explicit signal. `IsMultiTenantMode` is the boolean shortcut for code paths that need a hard branch.

### Component 2: Mode-aware audience scope enum

```csharp
public enum AudienceScope
{
    HostOperator,    // single-tenant: full-access operator
                     // MT: collapses to SaaSAdmin (cross-tenant access requires explicit opt-in)
    Tenant,          // MT only: tenant-scoped operator
                     // single-tenant: NOT a valid scope; resolves to HostOperator
    Customer,        // either mode: end-customer (own data only)
}
```

The audience enum has BOTH modes' values; the library's resolution logic collapses `Tenant` → `HostOperator` in single-tenant mode.

### Component 3: Query filter skip in single-tenant mode

```csharp
protected void ApplyTenantFilter<T>(ModelBuilder mb) where T : class, ITenantOwned
{
    if (!_identityProvider.IsMultiTenantMode) {
        // Single-tenant: skip the tenant filter entirely
        return;
    }
    // Capture an accessor lambda, NOT a value — the lambda is invoked per query,
    // so EF Core's filter sees the current scope's tenant ID rather than the one
    // that was current when OnModelCreating ran.
    Func<Guid> getCurrentTenantId = () => _identityProvider.CurrentTenantId.Value;
    mb.Entity<T>().HasQueryFilter(e => e.TenantId == getCurrentTenantId.Invoke());
}
```

In single-tenant mode, the global query filter is never registered. The library's data layer behaves as if there were no tenancy concept at all. No tenant-id column lookup on every query.

The lambda is captured at registration time but invoked per query, so EF Core's filter sees the current scope's tenant ID rather than the one that was current when `OnModelCreating` ran. A captured value (`var tid = _identityProvider.CurrentTenantId.Value`) would silently use the registration-time tenant for every subsequent query — a common bug that produces a quiet data-corruption hazard.

### Component 4: Bridge schema/UI auto-adaptation

The library's HTTP / GraphQL / Razor / Blazor surfaces inspect `IsMultiTenantMode` at registration time and ADAPT:

- GraphQL: schema in MT mode has tenant fields + `[AllowCrossTenant]` directive; same schema in single-tenant mode has those fields elided.
- Razor admin UI: in MT mode shows "current tenant" indicator + tenant switcher; in single-tenant mode hides them.
- REST API: in MT mode exposes `/api/tenants/...` routes; in single-tenant mode those routes return 404.

The adaptation is automatic from `IsMultiTenantMode`, not requiring host configuration.

### Component 5: Two integration packages

For each library that needs both modes:

```
LibFoo                              ← lift-safe core; uses ILibraryIdentityProvider abstraction; works in both modes
LibFoo.SingleTenant.AspNetCore      ← lift-safe bridge for single-tenant ASP.NET Core hosts;
                                       impl reads HttpContext.User; returns None for tenant
LibFoo.MultiTenant.Finbuckle        ← (the original MT-aware bridge for hosts using Finbuckle)
```

The bridges differ in HOW they populate `ILibraryIdentityProvider`; the library core doesn't care.

**A note on naming**: the abstract example above uses `.SingleTenant.` and `.MultiTenant.` as explicit tier prefixes. PolarSharp's wallet implementation uses a variation — `PolarSharp.PrepaidWallets.AspNetCore.*` for the single-tenant bridges (no `.SingleTenant.` infix) and `PolarSharp.PrepaidWallets.Polar.*` for the multi-tenant Polar bridges. The variation exists because the wallet ALSO conforms to Case Study 01's lift-shift contract — packages without a `.Polar.` infix are lift-safe and move to a new repo on a future lift. The `.SingleTenant.` infix would itself be a PolarSharp-specific naming convention, which would conflict with the goal of the lift-safe core being conceptually free of PolarSharp branding. Both naming patterns are valid; pick the wallet variation (`AspNetCore.*` for ST bridges) if your library ALSO needs lift-shift readiness, and the `.SingleTenant.` / `.MultiTenant.` explicit prefix pattern if liftability is not a goal for your library.

### Component 6: Auto-detection of MT registration (the "did the host opt in?" check)

The bridges can auto-detect which mode the host is running in by inspecting the DI container at startup. The wallet's `Polar.GraphQL` bridge does this: at registration time, it checks whether `PolarSharp.MultiTenant.*` services are registered. If yes, it exposes the full MT schema (tenant fields + cross-tenant directive). If no, it auto-degrades to the single-tenant schema slice and emits a Warning Log recommending the lighter `AspNetCore.GraphQL` package for that deployment.

The pattern: at bridge registration time, query `IServiceProvider` (or its build-time equivalent) for the marker service of the optional dependency. Branch behavior based on presence. Log a Warning if the bridge selection is suboptimal for the detected mode.

```csharp
// In the bridge's AddXxx extension:
public static IServiceCollection AddPolarWalletGraphQLBridge(this IServiceCollection services)
{
    services.AddHostedService<WalletGraphQLConfigurationCheck>();
    // ... main registration
    return services;
}

// Hosted service runs at startup; detects mode + warns:
internal sealed class WalletGraphQLConfigurationCheck : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WalletGraphQLConfigurationCheck> _logger;

    public Task StartAsync(CancellationToken ct)
    {
        var mtRegistered = _services.GetService<IMultiTenantContextAccessor>() is not null;
        if (!mtRegistered)
        {
            _logger.LogWarning(
                "Polar.GraphQL bridge detected NO MultiTenant registration. Schema gracefully degrades to single-tenant slice. Consider installing the lighter PolarSharp.PrepaidWallets.AspNetCore.GraphQL instead.");
        }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

This makes "picking the wrong bridge package" a recoverable startup warning rather than a confusing silent misbehavior.

## Implementation mechanics

### Step 1: Refactor your identity / context abstraction to be mode-agnostic

If your library currently has:

```csharp
// Old, MT-required:
public interface ILibraryContextProvider
{
    Guid CurrentUserId { get; }
    Guid CurrentTenantId { get; }    // ← always present; single-tenant hosts pass a dummy
}
```

Replace with:

```csharp
// New, mode-agnostic:
public interface ILibraryContextProvider
{
    Guid CurrentUserId { get; }
    Option<Guid> CurrentTenantId { get; }   // ← None in single-tenant
    bool IsMultiTenantMode { get; }
}
```

Update all consumers to check `IsMultiTenantMode` (or `CurrentTenantId.HasValue`) before doing tenant-scoped work.

### Step 2: Refactor query filters to skip in single-tenant mode

```csharp
// Before:
mb.Entity<Foo>().HasQueryFilter(e => e.TenantId == _currentTenantId);

// After: capture an accessor lambda, not a value
if (_identityProvider.IsMultiTenantMode) {
    Func<Guid> getCurrentTenantId = () => _identityProvider.CurrentTenantId.Value;
    mb.Entity<Foo>().HasQueryFilter(e => e.TenantId == getCurrentTenantId.Invoke());
}
// In single-tenant mode: no filter; data layer is tenancy-unaware
```

The lambda is captured at registration time but invoked per query, so EF Core's filter sees the current scope's tenant ID rather than the one that was current when `OnModelCreating` ran. A captured value (`var tid = _identityProvider.CurrentTenantId.Value`) would silently use the registration-time tenant for every subsequent query — a common bug that produces a quiet data-corruption hazard.

### Step 3: Refactor audience enums to support collapse

```csharp
// Library exposes 3 audiences:
public enum AudienceScope { HostOperator, Tenant, Customer }

// Library's resolution layer collapses:
public AudienceScope ResolveActualScope(AudienceScope requestedScope)
{
    if (!_identityProvider.IsMultiTenantMode && requestedScope == AudienceScope.Tenant) {
        return AudienceScope.HostOperator;
    }
    return requestedScope;
}
```

### Step 4: Adapt UI / API surfaces automatically

For GraphQL:

```csharp
public class LibraryGraphQlExtension : ITypeInterceptor
{
    public override void OnAfterCompleteType(ITypeDiscoveryContext ctx, ...)
    {
        if (!_identityProvider.IsMultiTenantMode) {
            // Remove tenant-related fields from the generated schema
            ctx.Type.Fields.Remove(f => f.Name.StartsWith("tenant"));
        }
    }
}
```

For Razor admin UI:

```cshtml
@if (IdentityProvider.IsMultiTenantMode)
{
    <div class="tenant-switcher">
        Current tenant: @CurrentTenant.Name <a asp-action="SwitchTenant">Switch</a>
    </div>
}
```

### Step 5: Two bridges, one core

Ship the library as:

```
LibFoo                                    NuGet package; lift-safe; the core
LibFoo.AspNetCore.Identity.SingleTenant   NuGet package; lift-safe; the single-tenant bridge
LibFoo.MultiTenant.Finbuckle              NuGet package; bridge to the MT host's tenancy infra
```

Host installs whichever bridge matches their deployment. The core works identically with either.

### Step 6: CI tests in both modes

```
tests/
  LibFoo.Tests.SingleTenantMode/        ← all behavioral tests with IsMultiTenantMode=false
  LibFoo.Tests.MultiTenantMode/         ← all behavioral tests with IsMultiTenantMode=true
  LibFoo.Tests.BothModes/                ← tests that should produce identical results either way
```

The matrix doubles the test count but exercises both paths. CI runs all three.

## Worked example (from PolarSharp)

PolarSharp.PrepaidWallets v1.3 is the reference implementation:

**Wallet-core abstractions (mode-agnostic):**
- `IWalletIdentityProvider.CurrentUserId : Guid` (required)
- `IWalletIdentityProvider.CurrentTenantId : Option<Guid>` (None in single-tenant)
- `IWalletIdentityProvider.IsMultiTenantMode : bool`
- `WalletAudienceScope { HostOperator, Tenant, Customer }` enum with auto-collapse logic

**Bridges:**
- `PolarSharp.PrepaidWallets.AspNetCore.Identity` — lift-safe single-tenant bridge; reads `HttpContext.User`; returns None for tenant; `IsMultiTenantMode = false`
- `PolarSharp.PrepaidWallets.AspNetCore.GraphQL` — lift-safe single-tenant GraphQL bridge; schema elides tenant fields
- `PolarSharp.PrepaidWallets.Polar.Identity` — Polar-coupled MT bridge using `PolarSharp.MultiTenant.Identity`; populates `CurrentTenantId.Some()` from Finbuckle context; `IsMultiTenantMode = true`
- `PolarSharp.PrepaidWallets.Polar.GraphQL` — Polar-coupled MT bridge; inspects MT registration; auto-degrades to single-tenant schema slice + emits Warning Log if MT isn't registered (see Component 6 for the detection pattern itself)

**Audience collapse:**
- MT mode + AppMasterAdmin user → `WalletAudienceScope.HostOperator` (cross-tenant via opt-in)
- MT mode + TenantAdmin → `WalletAudienceScope.Tenant`
- MT mode + Customer → `WalletAudienceScope.Customer`
- Single-tenant + any operator → `WalletAudienceScope.HostOperator`
- Single-tenant + Customer → `WalletAudienceScope.Customer`

The `Tenant` audience NEVER appears in single-tenant deployments. The reporting / GraphQL / admin UI surfaces automatically adapt.

**Query filter skip:**

The wallet's EF Core DbContext is designed to apply tenant filter only when `IsMultiTenantMode = true`. Single-tenant deployments are designed to use the same DbContext without tenant filtering overhead.

**Test matrix:**

Two integration test apps: `PolarPrepaidWalletsTestApp.MultiTenant` (uses Polar bridges + PolarSharp.MultiTenant.*) and `PolarPrepaidWalletsTestApp.SingleTenant` (uses AspNetCore bridges; no MultiTenant.*). Both are designed to exercise the SAME core wallet feature set; the multi-tenant test app exists today and the single-tenant test app is planned for v1.3.x.

## Trade-offs

**What you give up:**

1. **Slightly more friction in mostly-MT codebases.** Even MT-dominant projects must use `Option<TenantId>` rather than `TenantId`, and check `IsMultiTenantMode` in some paths. Marginal but real.
2. **Multiple bridges to maintain — scope-dependent.** For a simple library that just needs identity + UI integration, two bridges (one ST + one MT) may suffice. For a feature with many distinct concerns — catalog, payments, reporting, GraphQL, notifications, templates, etc. — the count grows to 8-12+ as each integration concern gets its own bridge package. The wallet ships 10 (8 Polar bridges spanning Identity / Checkout / Reporting / GraphQL / PurchaseOrder / Notifications / Translation / SaaSInvoicing + 2 single-tenant AspNetCore bridges for AspNetCore.Identity / AspNetCore.GraphQL). Plan the bridge inventory upfront so the maintenance scope is visible from day one.
3. **Audience collapse logic is non-obvious.** New contributors need to learn "what does `Tenant` mean in single-tenant mode?" before working with the audience enum.
4. **Test matrix doubles.** All behavioral tests run twice (once per mode).
5. **Cross-mode bugs are subtle.** A bug that only manifests in one mode is easy to miss without disciplined test coverage.

**Alternative patterns and why this one over those:**

- **Two separate libraries** — doubled maintenance; shared-bug-drift; documentation × 2; upgrade friction. Mode-agnostic core wins.
- **MT-only library + force single-tenant to use a "null" tenant** — works but feels heavy for single-tenant; some MT-specific features don't make sense in single-tenant. Mode-agnostic with feature-elision is cleaner.
- **Conditional compilation (`#if SINGLE_TENANT`)** — produces two binaries from one source; loses the runtime flexibility (a host can't switch modes via config). Not recommended.

## Failure modes

**Mode 1: Bridge misconfiguration — single-tenant bridge in an MT deployment.** Host installs the SingleTenant bridge but actually has multiple tenants. Symptoms: data from different tenants visible to each other. **Detection**: integration tests; the auto-detection logic in `Polar.GraphQL` etc. logs a Warning if it detects MT registration alongside the wrong bridge. **Recovery**: install the correct bridge; possibly migrate data.

**Mode 2: Query filter accidentally applied in single-tenant mode.** Bug in the filter logic still registers the filter even when `IsMultiTenantMode=false`; single-tenant queries return empty results because the filter compares against `null` tenant. **Detection**: integration tests in single-tenant mode. **Recovery**: fix the filter registration logic.

**Mode 3: Audience scope not collapsing.** Code path checks for `AudienceScope.Tenant` in single-tenant mode but the collapse hasn't run. **Detection**: behavioral test asserts "in single-tenant mode, no code path ever sees AudienceScope.Tenant." **Recovery**: add the collapse to the missing code path.

**Mode 4: Single-tenant host pays MT overhead anyway.** Some library code unconditionally calls `_identityProvider.CurrentTenantId.Value` and throws. **Detection**: profiling; integration tests. **Recovery**: refactor to check `.HasValue` or `IsMultiTenantMode` first.

**Mode 5: Schema elision incomplete.** Single-tenant GraphQL schema still exposes some tenant fields. **Detection**: schema-snapshot tests in both modes; visual review of generated schemas. **Recovery**: tighten the elision logic.

## When to use this elsewhere

**Signs this pattern fits your project:**

- You're authoring a library or framework component that will be used by 5+ external projects.
- Some of those projects are single-tenant (one company using the feature for themselves).
- Other projects are multi-tenant (a SaaS where end-users are tenants).
- You don't want to maintain two parallel codebases.
- You want users to be able to upgrade from single-tenant to multi-tenant without switching libraries.

**Signs this pattern is overkill:**

- The library is ONLY ever used by your own first-party application (you control the deployment mode).
- The library is intrinsically MT (e.g., a CRM platform that doesn't make sense for single-tenant).
- The library is intrinsically single-tenant (e.g., a desktop application).
- Two libraries is genuinely cheaper than mode-agnosticism (rare but possible for very simple libraries).

## Adaptation checklist

1. **Decide the abstraction shape FIRST.** Before any code, define your `ILibraryIdentityProvider`-equivalent interface with `Option<TenantId>` + `IsMultiTenantMode`. Get this design right.
2. **Define your audience enum to include both modes' values.** Document the collapse rules in code comments.
3. **Refactor every query filter to skip in single-tenant mode.** Don't just "register a filter that matches all rows" — actually skip the filter registration.
4. **Refactor every audience-aware code path to use the collapse logic.** Never assume `AudienceScope.Tenant` exists in single-tenant mode.
5. **Build the auto-adaptation into your UI / API surfaces.** Don't make hosts configure schema elision; do it automatically from `IsMultiTenantMode`.
6. **Ship two bridges from day one.** The single-tenant bridge is a feature, not an afterthought.
7. **Write integration tests in BOTH modes.** Single-tenant test app + multi-tenant test app; both run in CI; both exercise the same feature set.
8. **Document the upgrade path.** How does a single-tenant deployment become multi-tenant? Backfill TenantId on existing rows? Migration scripts? Test the path explicitly.
9. **Watch for "mode-specific surprises" in code review.** A `if (IsMultiTenantMode)` branch should be a smell triggering review — most code paths shouldn't need it. The collapse logic + query-filter-skip should cover most cases.
10. **Profile single-tenant deployments.** Verify the MT overhead is genuinely absent. Look for unnecessary tenant-id lookups, middleware execution, etc.
11. **Don't expose `IsMultiTenantMode` to end users.** It's an internal library concern; users see different UI / API surfaces in each mode but they don't need to know the flag exists.
12. **Plan for the eventual "yes, support multi-organization tenancy too" request.** Some hosts will eventually want THREE levels (host > organizations > users within org). Mode-agnostic libraries adapt to this gracefully; mode-locked libraries don't.
13. **Targeted tests for the collapse logic.** Beyond running all tests in both modes (item 7), add specific tests asserting the collapse invariants: in single-tenant mode, no code path ever sees `AudienceScope.Tenant`; the query filter is never registered; the schema does not expose tenant fields; the cross-tenant route returns 404. Without these targeted tests, the collapse logic can quietly stop working and the both-mode test runs may not catch it (each mode's tests verify their own behavior in isolation).

## Discussion / open questions

- **Should the library expose `IsMultiTenantMode` as a public read-only property?** Helpful for downstream code that needs to make mode-aware decisions. But it leaks an internal concern. Compromise: expose via `ILibraryIdentityProvider` (which is already a public abstraction) and treat as advanced API.
- **What about hybrid mode — single-tenant most of the time, MT for specific scenarios?** E.g., a library used by a single-tenant host that occasionally needs to query a "shared" external tenant's data. The pattern doesn't directly address this; would require a third "ExternalTenantContext" abstraction layered on top.
- **How to handle data migration from single-tenant to MT?** Single-tenant data has no TenantId column (or has a null one). MT-mode requires TenantId. The library should ship a migration helper that backfills TenantId on existing rows on first MT-mode startup, but the helper does not yet exist at the time of this case study. Design considerations the helper would need to address: idempotency (running twice should not double-stamp); downtime tolerance (whether the host can take maintenance windows for the backfill or needs an online migration); in-flight transactions (mid-backfill data state); how to choose the default tenant (the first user's organization? A literal "default" tenant? Configurable?); how to handle entities that span the single-tenant → MT transition mid-write. Worth dedicated treatment in v1.x.
- **Is `Option<T>` better than `Nullable<T>` for the tenant ID?** Stylistic. C# `Nullable<Guid>` is shorter; `Option<Guid>` is more explicit about the "this is meaningfully absent" semantics. PolarSharp uses `Option<T>` for consistency with its functional-style API surface.
- **Cross-feature consistency.** If a host installs Library A in single-tenant mode and Library B in MT mode, the mismatch is bad. Libraries can detect this at startup and emit a Warning, but enforcement is host-developer responsibility.

## Related patterns

- **Case Study 01 — Lift-and-Shift Architecture** — mode-agnostic core packages are naturally lift-safe (no PolarSharp.MultiTenant dependency); the two patterns reinforce each other.
- **Case Study 02 — Event-Sourced Wallet** — uses mode-agnostic identity abstractions; the entire wallet feature is built on this pattern.
- **Case Study 04 — Audience-Scoped Schema Slicing** — uses the audience collapse logic (Tenant → HostOperator in single-tenant) to compute schema slices that adapt to the deployment mode.
- **Ports and Adapters (Hexagonal Architecture)** — mode-agnostic abstractions are conceptually adjacent to ports; the bridges are the adapters. This pattern adds the mode-collapse + auto-adaptation dimensions.
- **Null Object Pattern** — `Option<TenantId>` returning `None` in single-tenant mode is conceptually a Null Object for "the tenant scope that doesn't exist here." The library treats `None` as a valid value rather than as an error.
- **Optional / Maybe / Option types** — the `Option<TenantId>` type is the foundation that lets mode-agnostic code stay clean. It forces every consumer to acknowledge the absence case rather than relying on sentinel values or null-reference checks scattered through the codebase.
- **Feature Flags** — `IsMultiTenantMode` is conceptually a feature flag chosen at registration time (not runtime). The library's behavior diverges based on the flag value, the same way feature-flag-conditioned code diverges based on a flag's setting.

## Citation format

> Chipman, Mark. *Multi-Tenancy as Optional for Library Authors*. PolarSharp Architectural Case Study 05. Molls and Hersh, LLC, 2026. https://github.com/mollsandhersh/Polar.sh_Nuget/tree/main/Case%20Studies
