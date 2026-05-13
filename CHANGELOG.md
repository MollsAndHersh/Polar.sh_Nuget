# Changelog

All notable changes to PolarSharp are documented here.
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
and [Common Changelog](https://common-changelog.org) format.

## [Unreleased]

## [1.2.1] — 2026-05-13

Patch release. Fixes two release-time gaps discovered after v1.2.0 shipped:

### Added

- `PolarSharp.EcommerceStoreManagement.Translation.Gemini` (1.0.0) — pack step added to CI. The package was implemented in Phase 7 of the v1.2.0 cycle but the workflow's `dotnet pack` list omitted it, so it never published. First publication to GitHub Packages happens at v1.2.1.
- `PolarSharp.EcommerceStoreManagement.Translation.Grok` (1.0.0) — same as Gemini above; first publication at v1.2.1.

### Fixed

- DocFX site navigation bar at https://mollsandhersh.github.io/Polar.sh_Nuget/ was empty (logo + search only). The classic template renders top-level `toc.yml` items in a sidebar that is hidden on the homepage's full-width layout, so the "Articles" and "API Reference" links were unreachable. Switched `docfx.json` to `"template": ["default", "modern"]`, which renders top-level items in the top navbar on every page.

### Changed

- `PolarSharp`, `PolarSharp.Webhooks`, `PolarSharp.MultiTenant`, `PolarSharp.Templates` version bump 1.2.0 → 1.2.1. No public API change vs 1.2.0; the bump exists solely to ship the CI + docs fixes through the tag-triggered publish job.

## [1.2.0] — 2026-05-13

This is a major feature release adding **25 new packages** that extend PolarSharp from a Polar.sh SDK into a full multi-tenant SaaS ecosystem: SQL-backed tenant + identity stores, programmatic + wizard-style merchant onboarding, an end-to-end ecommerce store-management layer with AI translation, hierarchical reporting, bulk fake-data seeding, and optional KeyCloak SSO. The v1.1.0 packages are **additive-compatible** — existing consumer code continues to work; new capabilities are opt-in via package install.

### Added

#### `PolarSharp.BaseEntities` (new, 1.0.0)

- 15 Polar-native abstract record bases mirroring Polar's webhook wire format: `PolarTenantBase`, `PolarCustomerBase`, `PolarProductBase`, `PolarPriceBase`, `PolarOrderBase`, `PolarOrderLineItemBase`, `PolarSubscriptionBase`, `PolarRefundBase`, `PolarDiscountBase`, `PolarBenefitBase`, `PolarBenefitGrantBase`, `PolarCheckoutBase`, `PolarLicenseKeyBase`, `PolarAddressBase`, `PolarMediaFileBase`
- 6 host-additive abstract record bases: `PolarShoppingCartBase`, `PolarCartLineItemBase`, `PolarCategoryBase`, `PolarDepartmentBase`, `PolarInventoryRecordBase`, `PolarSaleBase`
- 10 wire-format enums with `[JsonStringEnumConverter]`: `PolarOrderStatus`, `PolarSubscriptionStatus`, `PolarRefundStatus`, `PolarRefundReason`, `PolarCheckoutStatus`, `PolarLicenseKeyStatus`, `PolarRecurringInterval`, `PolarTrialInterval`, `PolarOrganizationStatus`, `PolarBenefitType`
- 4 core interfaces: `IPolarEntity`, `IPolarTimestamped`, `IPolarMetadata`, `IPolarOrganizationScoped`
- Zero external dependencies; AOT-safe; targets net10.0

#### SQL-backed tenant store (4 new packages)

- `PolarSharp.MultiTenant.EntityFrameworkCore` (1.0.0) — common base with `TenantAwareDbContextBase`, `ITenantOwned`, `IPolarTenantCache` (Memory + Distributed impls), `IPolarTenantScopeInitializer`, `IFakeDataPolicy`, dual tenant + fake-data global query filter, `PolarMigrationRunner<TContext>` hosted service
- `PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer` (1.0.0)
- `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite` (1.0.0) — shared `__tenants.db`; catalog/identity DBs use `{tenantId}.db` per tenant
- `PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL` (1.0.0)

#### Identity + RBAC/ABAC (4 new packages)

- `PolarSharp.MultiTenant.Identity` (1.0.0) — `PolarApplicationUser : IdentityUser<Guid>` with `IsAppMasterAdmin` flag, `PolarApplicationRole : IdentityRole<Guid>`, M:N `PolarUserTenantMembership` (a user can have different roles in different tenants), `PolarRoles` / `PolarClaims` constants, `PolarPermission` enum (22 fine-grained permissions), `[RequirePolarPermission]` / `[RequireAppMasterAdmin]` / `[AllowCrossTenant]` attributes, `ICurrentUser`, `IAppMasterAdminProvisioning`, `RoleSeeder`, `AppMasterAdminBootstrapper`, `TenantAdminInvariantValidator`, `PlatformAuditLogEntry` (site-level cross-tenant audit ledger), three deployment shapes (dedicated DB / shared DB / shared host DbContext)
- `PolarSharp.MultiTenant.Identity.SqlServer` (1.0.0)
- `PolarSharp.MultiTenant.Identity.Sqlite` (1.0.0)
- `PolarSharp.MultiTenant.Identity.PostgreSQL` (1.0.0)
- `TenantAdminAutoProvisioningPostProcessor` — auto-creates the first `TenantAdmin` membership on successful tenant onboarding

#### Tenant onboarding (1 new package)

- `PolarSharp.Onboarding` (1.0.0) — `IPolarOnboardingClient` with both **programmatic** (headless / B2B) and **OAuth-linking** (user-consent) flows, plus a **persistent wizard** API (`IOnboardingWizard`) with resumable sessions (7-day TTL), conditional next-steps based on prior answers (e.g. `TranslationConfig` step appears only when `RequiresMultiLanguage=true`), `OnboardingSessionExpirationCleaner` background prune, encrypted-at-rest in-flight translation API keys via the Data Protection API, `IOnboardedTenantSink` abstraction with `EfMultiTenantStoreSink` impl, `IOnboardingPostProcessor` extension point

#### Ecommerce store management (5 new packages + 5 translation provider packages)

- `PolarSharp.EcommerceStoreManagement` (1.0.0) — `LocalProduct` (with M:N `CategoryIds` — products can live in multiple categories simultaneously), `LocalProductVariant` (computed `IsOutOfStock` / `IsLowStock`), `LocalCategory`, `LocalDepartment`, `LocalTierGroup` + `TierLevel` (cumulative-benefit ladder), `LocalPrice` (5 price kinds incl. seat-tiered, recurring, trial), polymorphic `LocalBenefit` hierarchy (7 subtypes), `LocalDiscount`, `LocalCheckoutLinkConfig`, `TenantBusinessProfile` (encrypted translation API key), `AdminAuditLogEntry`; service abstractions: `IRefundService` / `ILicenseKeyValidator` / `IInventoryUpdater` / `IPolarBusinessProfileService` / `IAuditLogActorProvider`; `IPolarCatalogPublisher` with `PublishPlan` / `PublishReport` / sealed `PlannedAction` + `PublishOutcome` hierarchies; translation infrastructure (`IPolarCatalogTranslator`, `ITranslationProviderResolver` for 3-tier resolution, `IPolarCatalogTranslationCache` Memory + Distributed impls, `CatalogTranslationEntity` unified i18n table, `IPolarCatalogReader` reassembly API); **5 cloning services** (`IProductCloningService` / `ICategoryCloningService` / `IBenefitCloningService` / `IDiscountCloningService` / `ICheckoutLinkCloningService`) with built-in duplicate prevention via `" (Copy)"` auto-suffix and discount-code-null-by-default
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore` (1.0.0) — `PolarCatalogDbContext` with 12 entities + 12 `IEntityTypeConfiguration<T>` classes
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer` (1.0.0)
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite` (1.0.0)
- `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL` (1.0.0)
- **5 translation provider packages** using raw HttpClient + JSON (no third-party SDK transitive bulk; AOT-safe; shared prompt/parser):
  - `PolarSharp.EcommerceStoreManagement.Translation.Anthropic` (1.0.0)
  - `PolarSharp.EcommerceStoreManagement.Translation.OpenAI` (1.0.0)
  - `PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI` (1.0.0)
  - `PolarSharp.EcommerceStoreManagement.Translation.Gemini` (1.0.0)
  - `PolarSharp.EcommerceStoreManagement.Translation.Grok` (1.0.0)

#### Reporting (4 new packages)

- `PolarSharp.Reporting` (1.0.0) — `IPolarReportingClient` with aggregate KPI reports (`TransactionReport`, `SubscriptionReport`, `OrderReport`, `ErrorAuditReport`, `CustomerReport`, `CustomerEntitlementsReport`), JSON variants for every method, and **hierarchical drilldown** (`ListCustomersAsync` → `ListOrdersForCustomerAsync` → `GetOrderDrilldownAsync`) designed for Telerik / MudBlazor / Blazor hierarchical grids — each level paged independently; pre-aggregated columns (`OrderCount`, `LifetimeValue`, `LineItemCount`, `RefundedAmount`) load 10k-customer top-level grids in sub-100ms; `IReportSnapshotService` + `PolarReportingOptions` + `IReportExporter` (CSV/JSON streaming)
- `PolarSharp.Reporting.EntityFrameworkCore` (1.0.0) — `PolarReportingDbContext` with 8 snapshot entities + `EfPolarReportingClient`
- `PolarSharp.Reporting.EntityFrameworkCore.SqlServer` (1.0.0)
- `PolarSharp.Reporting.EntityFrameworkCore.Sqlite` (1.0.0)
- `PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL` (1.0.0)

#### Data seeding (1 new package)

- `PolarSharp.DataSeeding` (1.0.0) — `IPolarDataSeeder` with 6 Bogus generators (Product / Category / Department / LicenseKeysBenefit / Discount / CheckoutLink); `SeedScale` (Demo / QA / Stress) presets; deterministic seeding via `randomSeed`; `ISeedSink` abstraction; `FakeDataToggleChanged` event + `FakeDataSyncService` background reconciliation with Polar's sandbox

#### KeyCloak SSO (1 new package)

- `PolarSharp.MultiTenant.Identity.KeyCloak` (1.0.0) — optional OIDC SSO add-on; `KeyCloakClaimsTransformer` maps realm-roles → PolarSharp roles, emits AppMasterAdmin dual-flag, propagates tenant id; env-var client-secret resolution; idempotent claim rewriting

#### EF Core migrations — required per provider

- **12 real migration sets** generated across the SQL-backed packages × 3 providers (tenant store, Identity, Catalog, Reporting × SqlServer / Sqlite / PostgreSQL)
- `PolarMigrationRunner<TContext>` hosted service applies pending migrations idempotently on host startup; **Production startup throws** when migrations are missing (never silently `EnsureCreated` an unversioned schema); Dev-mode `EnsureCreatedAsync` fallback is opt-in
- `IDesignTimeDbContextFactory<T>` per (DbContext, provider) combination — `dotnet ef migrations add` works against each provider package

### Changed

- **`PolarSharp.MultiTenant.PolarTenantInfo` and the v1.1.0 `WebhookXxxData` records remain UNCHANGED** in 1.2.0 — the original plan called to un-seal them and add `PolarXxxBase` inheritance as "additive," but the wire shapes of the v1.1.0 records differ from the new bases (nullable vs required, string vs typed enum, nested vs FK-id). Making them inherit would be a v2.0-level breaking change. Deferred to v2.0; v1.1.0 webhook consumers are fully compatible with 1.2.0.

### Notes for hosts

- **AOT publish with `PolarSharp.DataSeeding`:** the bundled `PolarTestApp` does NOT transitively reference `PolarSharp.DataSeeding`, so `dotnet publish -p:PublishAot=true` against the test app stays clean. The `Bogus` faker library used by `PolarSharp.DataSeeding` does some reflection internally — hosts who publish AOT with `PolarSharp.DataSeeding` installed may see reflection / trim warnings. Two supported mitigations: (1) suppress the warnings only in the host's csproj via `<TrimmerRootAssembly Include="Bogus" />`, or (2) gate the `AddPolarDataSeeding(...)` registration behind `#if DEBUG` so the package compiles out of the Production build entirely. `PolarSharp.DataSeeding` is a dev-time package — designed for sandbox / QA / demo environments, not production hot paths — so either approach is acceptable.

### Tests

- **441 / 441 tests pass** across all 29 packages (4 from v1.1.0 + 25 new in v1.2.0)
- AOT publish: `dotnet publish -p:PublishAot=true` zero warnings
- Build: 0 warnings, 0 errors across the entire solution

## [1.1.0] — 2026-05-12

### Added

- **Standalone `PolarSharp.Webhooks` mode** — package now works independently without `PolarSharp` core or `PolarSharp.MultiTenant`
  - `AddPolarWebhooks(IServiceCollection, Action<PolarWebhookOptions>?)` — direct `IServiceCollection` extension; no `PolarInfrastructureBuilder` dependency
  - `MapPolarWebhooks(IEndpointRouteBuilder)` — registers the webhook POST endpoint directly on any Minimal API or MVC host
  - `PolarWebhooksBuilder` fluent type returned by `AddPolarWebhooks` for standalone handler registration
  - `testapp/PolarWebhooksTestApp` — complete reference application using only `PolarSharp.Webhooks`; all 28 handlers via `LoggingHandlerBase<TEvent>`

- **Webhook test suite** — `tests/PolarSharp.Webhooks.Tests/Standalone/` (70 tests total)
  - `StandaloneRegistrationTests` — verifies DI registration of validator, dispatcher, keyed route mapper, keyed rate-limiter activator, handler adapters (all 28)
  - `StandaloneOptionsTests` — verifies `PolarWebhookOptions` config binding from `IConfiguration`, multiple secrets, in-code configure overrides
  - `StandaloneStartupValidatorTests` — verifies host starts when all 28 handlers registered, warns without `FailOnMissingHandlers`, fails-fast with `FailOnMissingHandlers=true`, partial handlers enumerate missing names in exception message
  - `StandaloneHttpPipelineTests` — in-memory `TestServer` end-to-end pipeline: valid HMAC → 200, bad signature → 400, tampered body → 400, expired timestamp → 400, missing signature header → 400, wrong content-type → 415, wrong path → 404, GET → 405, secret rotation with two simultaneous secrets → 200

- **Integration test suite** — `tests/PolarSharp.IntegrationTests/Standalone/` (48 tests total)
  - `StandaloneWebhookPipelineTests` — `WebApplicationFactory<Program>` against real `PolarWebhooksTestApp`; Theory across all 28 event types; 415/404/405/400 enforcement; multi-secret rotation scenario

- `docs/articles/webhook-event-reference.md` — generated reference listing all 28 webhook event types, their `type` discriminator strings, and available payload fields

### Fixed

- **`IWebhookTenantScopeInitializer` ASP.NET Core binding bug** — interface type was declared as a Minimal API parameter, causing `NotSupportedException: Deserialization of interface or abstract types is not supported` (HTTP 500) on every webhook POST in standalone deployments without `PolarSharp.MultiTenant` installed. Resolved by removing the parameter and resolving via `context.RequestServices.GetService<T>()` instead.

- **`UsePolarInfrastructure` webhook discovery** — previously gated behind `marker.WebhooksRegistered`; now always attempts keyed DI lookup so standalone `MapPolarWebhooks()` usage works even when the core marker is not set.

### Changed

- `PolarSharp` → 1.1.0, `PolarSharp.Webhooks` → 1.1.0, `PolarSharp.MultiTenant` → 1.1.0

## [1.0.0] — 2026-05-01

### Added

- `PolarSharp` — core HTTP client package targeting Polar.sh API
  - `PolarClient` singleton wrapping Kiota-generated `PolarApiClient`
  - Typed resource properties for all 25+ Polar API areas (Orders, Subscriptions, Customers, Products, Checkouts, Benefits, Refunds, Discounts, Meters, License Keys, Customer Portal, etc.)
  - `Result<TValue, TError>` and `Option<T>` monads for error handling without exceptions on 4xx responses
  - `PolarError` sealed record hierarchy: `AuthenticationError`, `AuthorizationError`, `NotFoundError`, `ValidationError`, `RateLimitError`, `ServerError`
  - `BearerTokenHandler` — injects `Authorization: Bearer` header; scrubs token from all log output
  - `IdempotencyKeyHandler` — auto-generates `X-Idempotency-Key` on mutating requests; stable across retries
  - `ApiVersionHandler` — sends `Polar-Version` ISO-date header on every request via `IOptionsMonitor` (hot-reloadable)
  - `PolarResilienceHandler` — retry + circuit breaker + timeout via `Microsoft.Extensions.Http.Resilience`
  - `PolarSocketsHandlerFactory` — configures `SocketsHttpHandler` with per-host connection pool, DNS rotation, HTTP/2 multiplexing, TLS hardening
  - `PolarActivitySource` — distributed tracing spans via `System.Diagnostics.ActivitySource`
  - `PolarMeter` — metrics via `IMeterFactory`: request count, error rate, latency histogram, inflight gauge, webhook counters
  - `PolarHealthCheck` — `IHealthCheck` pinging `GET /v1/organizations`; returns `Healthy`/`Degraded`/`Unhealthy`
  - `PolarCustomerPortalClient` — separate `HttpClient` for Customer Access Token endpoints; hard security boundary from OAT client
  - `PaginatedList<T>` and `PaginationExtensions.ToAsyncEnumerable<T>` auto-page iterator
  - `IPolarLocalizer` public interface; built-in `PolarResourceLocalizer` backed by `en-US` and `es-MX` embedded `.resx` files
  - `PolarJsonContext` source-generated `JsonSerializerContext` for AOT-safe serialization
  - `ResultExtensions.ToHttpResult` Minimal API helper mapping `Result<T, PolarError>` to `IResult`
  - `PolarPiiRedactor` — redacts customer emails, names, and error details from structured log scopes
  - Startup mode banner (Test vs Live) and token-prefix sanity check
  - `PolarOptionsValidator` — explicit `IValidateOptions<PolarOptions>` (zero reflection; AOT-safe) with `ValidateOnStart()`
  - `LazyConcurrentDictionary<TKey, TValue>` — race-free per-key cache via `ConcurrentDictionary<TKey, Lazy<TValue>>`
  - `AddPolarInfrastructure` / `UsePolarInfrastructure` DI and middleware extension methods

- `PolarSharp.Webhooks` — webhook verification and event handling package
  - `WebhookValidator` — HMAC-SHA256 signature verification per Standard Webhooks spec; multi-secret rotation; constant-time comparison
  - `WebhookEvent` abstract base; 28 `sealed record` event types (all Polar event categories)
  - `IPolarWebhookHandler<TEvent>` interface and `PolarWebhookHandlerBase<TEvent>` abstract base class
  - `IPolarWebhookDispatcher` — routes verified events to registered scoped handlers
  - `PolarWebhookStartupValidator` `IHostedService` — handler completeness check at startup against `KnownWebhookEventTypes.All`
  - `FailOnMissingHandlers` config flag — fails startup if any event type has no handler
  - `enqueue: true` flag on `AddWebhookHandler<T>` — wraps handler in bounded `Channel<T>` + `IHostedService` for slow handlers
  - `[ValidatePolarWebhook]` MVC action filter for controller-based webhook handling
  - `IPolarToastChannel` — bounded `Channel<PolarToastNotification>` for real-time UI notifications
  - `PolarToastNotification` rich record with 25+ typed fields plus lazy localization via `Localize(IPolarLocalizer)`
  - `PolarWebhookReconciler` `IHostedService` — periodic replay of missed events via `polar.Events.ListAsync(since:)`
  - Webhook security: payload size cap (1 MB), ASP.NET Core rate limiting, IP allowlisting (opt-in), HTTPS enforcement (400 not redirect), content-type enforcement, timing-uniform error responses
  - `PolarWebhookMessageKeys` and `PolarWebhookMessages.resx` / `PolarWebhookMessages.es-MX.resx` with 100% key coverage for both cultures

- `PolarSharp.MultiTenant` — Finbuckle-backed multi-tenant integration package
  - `PolarTenantInfo : ITenantInfo` with `PolarAccessToken` and `Server` per tenant
  - `IMultiTenantPolarClientFactory` / `MultiTenantPolarClientFactory` — per-tenant `PolarClient` with race-free `LazyConcurrentDictionary` caching
  - **Per-tenant bulkhead isolation**: each tenant gets a dedicated `SocketsHttpHandler` (own connection pool) + `TenantResilienceDelegatingHandler` (own `ResiliencePipeline<HttpResponseMessage>` with independent circuit breaker and retry state)
  - Finbuckle strategies: `Header`, `Route`, `Hostname`, `Claim` — configured from `PolarSharp:MultiTenant` in `appsettings.json`
  - `IAsyncDisposable` on factory — disposes all per-tenant `HttpClient` instances on graceful shutdown

- **Cross-cutting**
  - All libraries target `net10.0` with `IsAotCompatible=true` and `IsTrimmable=true`; CI gates `dotnet publish -p:PublishAot=true` with zero warnings
  - `<Deterministic>true</Deterministic>` + `<ContinuousIntegrationBuild>` — reproducible builds
  - `Microsoft.VisualStudio.Threading.Analyzers` with `VSTHRD111` as build error — `ConfigureAwait(false)` enforced across all library code
  - `WarningsAsErrors=CS1591` — all public members must have XML doc comments
  - `Microsoft.SourceLink.GitHub` — NuGet consumers can step through source in debugger
  - Public API surface snapshot tests (via `PublicApiGenerator` + `Verify.Xunit`) — breaking changes to `PolarSharp` and `PolarSharp.Webhooks` assemblies are caught at PR time
  - 66 unit tests across `PolarSharp.Tests`, `PolarSharp.Webhooks.Tests`, and `PolarSharp.IntegrationTests`

[Unreleased]: https://github.com/markchipman/PolarSharp/compare/HEAD
