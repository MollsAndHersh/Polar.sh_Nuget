# PolarSharp

A .NET 10 Native AOT-compatible SDK for [Polar.sh](https://polar.sh) — the open-source Merchant of Record payment and monetization platform.

> **v1.2.0+** — what started as a Polar.sh SDK has grown into a full multi-tenant SaaS toolkit. **31 packages** cover everything from the raw API client to programmatic merchant onboarding, SQL-backed tenant + identity stores with row-level security, a local catalog with AI-translated product copy, hierarchical reporting, KeyCloak SSO, and bulk fake-data seeding. Pick the pieces you need; the v1.1.0 core SDK keeps working unchanged for hosts that don't.

## What You Get

**31 NuGet packages**, every one AOT-safe, grouped into seven capability areas:

| Capability area | Packages | What it does |
|---|---|---|
| **Core SDK + webhooks + client isolation** | `PolarSharp`, `PolarSharp.Webhooks`, `PolarSharp.MultiTenant`, `PolarSharp.Templates` | The v1.1.0 origin story. Full Polar.sh API client, HMAC-verified event handling, per-tenant `HttpClient` isolation with independent circuit breakers, `dotnet new` handler scaffolds. |
| **Universal domain model** | `PolarSharp.BaseEntities` | 15 abstract record bases that mirror Polar's webhook wire format byte-for-byte (Order, Customer, Product, Subscription, etc.) + 6 host-additive bases (Cart, Category, Department, Inventory, Sale). Inherit them in your own types and webhook payloads stop needing translation. |
| **SQL-backed tenant store** | `PolarSharp.MultiTenant.EntityFrameworkCore` + `.SqlServer` / `.Sqlite` / `.PostgreSQL` | When `appsettings.json` isn't enough. SQL Server + PostgreSQL get row-level security with an AppMasterAdmin bypass; SQLite gets one `.db` file per tenant for physical isolation. EF Core query filters + RLS = defense in depth. |
| **Identity + SSO** | `PolarSharp.MultiTenant.Identity` + `.SqlServer` / `.Sqlite` / `.PostgreSQL` + `.KeyCloak` | ASP.NET Core IdentityFramework with Guid IDs, M:N user↔tenant memberships, a site-level `AppMasterAdmin` tier orthogonal to tenant scope, five-layer cross-tenant safeguards, optional KeyCloak SSO via OIDC. |
| **Tenant onboarding** | `PolarSharp.Onboarding` | Single-call programmatic API (POST `/v1/organizations/` + OAT + webhook endpoint) **and** a resumable wizard with persistent sessions for interactive UI flows. Encrypts in-flight translation API keys at rest. Auto-provisions a TenantAdmin on completion. |
| **Local catalog + AI translation** | `PolarSharp.EcommerceStoreManagement` (+ EF Core + 3 providers) + `.Translation.Anthropic` / `.OpenAI` / `.AzureOpenAI` / `.Gemini` / `.Grok` | Author products, variants, categories, tier groups, discounts, and checkout links locally; publish to Polar idempotently with variant + tier expansion. Three-tier translation provider resolution (per-tenant BYOK → master → disabled). Refund service, license validator, inventory sync, admin audit log. |
| **Reporting** | `PolarSharp.Reporting` (+ EF Core + 3 providers) | Aggregate KPI reports (revenue, MRR, churn, fulfillment latency) **plus** lazy three-level hierarchical drilldown — Customers → Orders → Order details. Pre-aggregated columns keep a 10k-customer top-level grid loading in <100ms. Optional snapshot service mirrors Polar to local SQL on a schedule. |
| **Fake data for sandbox / QA** | `PolarSharp.DataSeeding` | Bogus-backed bulk generators at Demo / QA / Stress scales. Every fake record is tagged `IsFakeData=true` and filtered by a global EF query predicate; flip a per-tenant toggle to sync fake data to/from Polar's sandbox. |

## Installation

The full list of 31 packages with version badges is in the [GitHub repo README](https://github.com/MollsAndHersh/Polar.sh_Nuget#all-31-packages). Common install paths:

**Core SDK** (the only required package):

```bash
dotnet add package PolarSharp
```

**Common opt-ins**:

```bash
dotnet add package PolarSharp.Webhooks      # HMAC verification, toast notifications, background queues, reconciliation
dotnet add package PolarSharp.MultiTenant   # per-tenant HttpClient isolation with Finbuckle
dotnet new install PolarSharp.Templates     # dotnet new templates for scaffolding webhook handlers
dotnet add package PolarSharp.BaseEntities  # universal domain bases — Order, Customer, Product, etc.
```

**SQL-backed tenant + identity stores** (pick the provider that matches your DB):

```bash
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer       # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.MultiTenant.Identity.SqlServer                  # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.MultiTenant.Identity.KeyCloak                   # optional OIDC SSO add-on
```

**Tenant onboarding and local catalog**:

```bash
dotnet add package PolarSharp.Onboarding
dotnet add package PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer    # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.EcommerceStoreManagement.Translation.Anthropic            # or .OpenAI / .AzureOpenAI / .Gemini / .Grok
```

**Reporting and data seeding**:

```bash
dotnet add package PolarSharp.Reporting.EntityFrameworkCore.SqlServer    # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.DataSeeding                                # dev-time only; see Data Seeding article for AOT note
```

Every package depends transitively on the ones it needs — installing a provider package brings in its EF Core base and PolarSharp itself automatically. The packages are distributed via [GitHub Packages](https://github.com/mollsandhersh/Polar.sh_Nuget/packages); see [NuGet Deployment](articles/nuget-deployment.md) for the `NuGet.config` + PAT setup.

## Quick Start

```csharp
// Program.cs
builder.Services
    .AddPolarInfrastructure(builder.Configuration);

// appsettings.json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx"
  }
}

// Inject and call
app.MapGet("/orders", async (PolarClient polar, CancellationToken ct) =>
    (await polar.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct))
        .Match(
            onSuccess: orders => Results.Ok(orders),
            onFailure: err    => err.ToHttpResult()));
```

For the full setup including webhook registration, multi-tenant configuration, identity bootstrap, and onboarding flows, see [Getting Started](articles/getting-started.md).

## Documentation

Every article is linked from the **Articles** menu in the top navigation. Highlights for the v1.2.0+ ecosystem:

- [Universal Domain Model (BaseEntities)](articles/base-entities.md) — the 15 + 6 abstract record bases and how host inheritance eliminates webhook mapping
- [Identity and Authorization](articles/identity-and-authorization.md) — 5-role RBAC + 22-permission ABAC, AppMasterAdmin tier, 5-layer cross-tenant safeguards
- [Tenant Onboarding](articles/onboarding.md) — programmatic single-call API + resumable wizard with conditional next-steps
- [Ecommerce Store Management](articles/ecommerce-catalog.md) — local catalog with variants + tiers, 3-tier translation, idempotent publish to Polar
- [Reporting](articles/reporting.md) — aggregate KPI reports + hierarchical drilldown for Telerik/MudBlazor grids
- [EF Core Migrations](articles/migrations.md) — 12 migration sets, `PolarMigrationRunner<TContext>`, production checklist
- [Data Seeding](articles/data-seeding.md) — Bogus generators, scale presets, `IsFakeData` toggle, Polar sandbox sync

**API Reference** — use the _API Reference_ link in the top navigation bar for the full XML-documented API surface across all 31 packages.

## Features

| Feature | Description |
|---|---|
| 31-package ecosystem | Core SDK + 27 opt-in packages across 7 capability areas, all AOT-safe |
| Full API surface | 25+ resource areas generated from the OpenAPI spec via Kiota |
| Native AOT | Zero reflection in hot paths; publishes with `dotnet publish -p:PublishAot=true` |
| Resilience | Retry, circuit breaker, timeout, hedging |
| Idempotency | Automatic `X-Idempotency-Key` on mutating requests |
| API versioning | `Polar-Version` date-pinned header; mismatch detection at startup |
| Test/Live mode | Startup banner + token-prefix sanity check |
| Result monad | `Result<TValue, PolarError>` — no exceptions on 4xx |
| Multi-tenant isolation | Per-tenant `HttpClient` + circuit breaker + connection pool + EF Core query filters + database RLS |
| Identity + SSO | ASP.NET Core IdentityFramework with M:N user↔tenant memberships; optional KeyCloak OIDC SSO |
| Health checks | `IHealthCheck` integration; tags `"polar"` (core) and `"polar-sql"` (EF Core) |
| Observability | `ActivitySource` + `IMeterFactory` metrics; structured logs with PII redaction |
| Localization | Built-in `en-US` and `es-MX`; extensible via `IPolarLocalizer` |
| EF Core migrations | 12 migration sets across 4 DbContexts × 3 SQL providers; `PolarMigrationRunner<TContext>` hosted service |
| Strong-named | Assemblies signed; NuGet package signing in CI |

## License

MIT — [View on GitHub](https://github.com/mollsandhersh/Polar.sh_Nuget)

---

> **Notice:** PolarSharp is an independent open-source project with no affiliation, partnership, or relationship of any kind with Polar.sh or its operators. This software is provided "as is" without warranty of any kind. Use is entirely at your own risk. See the [full disclaimer](articles/disclaimer.md) for complete terms.
