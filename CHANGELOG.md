# Changelog

All notable changes to PolarSharp are documented here.
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
and [Common Changelog](https://common-changelog.org) format.

## [Unreleased]

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
