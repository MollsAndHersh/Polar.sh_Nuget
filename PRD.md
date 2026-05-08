# PolarSharp — Product Requirements Document

| Field | Value |
|---|---|
| **Document version** | 1.0 |
| **Status** | Approved for implementation |
| **Last updated** | 2026-05-07 |
| **Owner** | Mark Chipman (markchipman@gmail.com) |
| **Engineering lead** | TBD |
| **Target release** | v1.0.0 |
| **Distribution channels** | NuGet.org (`PolarSharp`, `PolarSharp.Webhooks`, `PolarSharp.MultiTenant`, `PolarSharp.Templates`) |

---

## 1. Executive Summary

**PolarSharp** is a .NET 10 Native AOT-compatible Software Development Kit (SDK) for [Polar.sh](https://polar.sh), an open-source Merchant of Record (MOR) payment and monetization platform. It delivers full operational parity with Polar's official Python SDK while providing native idiomatic .NET integration, enterprise-grade resilience, multi-tenancy, real-time webhook handling, and first-class observability.

The library is delivered as four NuGet packages (one core + three optional) that together cover every operational dimension a serious .NET host application needs to integrate with a payment platform: HTTP client, authentication, resilience, idempotency, API versioning, webhook signature verification, multi-tenant request isolation, real-time UI notifications, and bilingual localization shipped out of the box.

PolarSharp is designed for enterprise adoption from day one: AOT-compatible (all hot paths reflection-free), strong-named and code-signed, fully snapshot-tested for API & wire-format stability, observable via OpenTelemetry, hardened against documented webhook attack vectors, and operationally safe via per-tenant bulkhead isolation, race-free concurrent state, and graceful shutdown semantics.

---

## 2. Background & Strategic Context

### 2.1 Market gap

Polar.sh ships a feature-complete, auto-generated Python SDK (`polar-python-sdk` via Speakeasy) but **no native .NET support**. .NET developers integrating Polar today must either:

1. Hand-roll an HTTP client against the OpenAPI spec — duplicating effort across teams, no shared resilience, no shared webhook verification, no shared observability conventions.
2. Use Polar's REST API directly via `HttpClient` — losing all the type safety and ergonomic patterns .NET developers expect.
3. Wait for an unofficial community wrapper — often abandoned, rarely AOT-safe, never enterprise-ready.

PolarSharp closes this gap with a generation-first approach (Kiota-generated client from Polar's OpenAPI spec) wrapped in a hand-curated, opinionated, ZoranHorvat-compliant public API.

### 2.2 Strategic alignment

The library targets the segment of the .NET ecosystem most likely to need MOR payment infrastructure:

- **B2B SaaS startups** moving up-market who need MOR for VAT/tax compliance.
- **Indie developers** monetizing software without setting up their own merchant infrastructure.
- **Enterprise platforms** with multi-tenant architectures who want a uniform .NET integration across many internal product teams.
- **AOT-targeting workloads** — Azure Container Apps, AWS Lambda .NET, Cloudflare Workers via `wasi-experimental`, where startup cost and binary size matter.

### 2.3 Differentiators vs. raw HTTP client integration

| Capability | Raw `HttpClient` | PolarSharp |
|---|---|---|
| Type-safe Polar API surface | ✗ | ✓ (180+ ops, 200+ models) |
| Bearer auth with hot-reload | manual | `IOptionsMonitor` + `BearerTokenHandler` |
| Idempotency keys on retries | manual | automatic via `IdempotencyKeyHandler` |
| API version pinning | manual | `Polar-Version` header via `ApiVersionHandler` |
| Resilience (retry/circuit-breaker/timeout) | manual | `Microsoft.Extensions.Http.Resilience` preconfigured |
| Webhook HMAC verification | manual | Standard Webhooks spec compliant, multi-secret rotation |
| Multi-tenant isolation | hand-roll bulkhead | per-tenant pipeline via `ResiliencePipelineRegistry` |
| Real-time UI events | hand-roll channel | `IPolarToastChannel` + lazy localization |
| AOT compatibility | depends on choices | guaranteed; CI-enforced |
| Observability | hand-roll | `ActivitySource` + `IMeterFactory` out of the box |

---

## 3. Goals & Non-Goals

### 3.1 Primary goals (P0)

1. **Full operational parity** with the Polar.sh Python SDK across all 25+ resource areas (Orders, Subscriptions, Customers, Products, Checkouts, Benefits, Refunds, Discounts, Meters, License Keys, Customer Portal, etc.).
2. **Native AOT compatibility** — the library must publish with `PublishAot=true` with zero warnings on .NET 10.
3. **Enterprise-grade resilience** — automatic retry, circuit breaker, rate-limit awareness, hedging for idempotent reads, all configurable.
4. **Webhook safety** — HMAC-SHA256 verification per Standard Webhooks spec, multi-secret rotation, replay-protection timestamp validation, timing-uniform error responses.
5. **Multi-tenant isolation** — one tenant's failures must not affect any other tenant's request budget, circuit-breaker state, or connection pool.
6. **Observability** — every API call traced via `ActivitySource`; every operation metered via `IMeterFactory`; structured logging with PII redaction.
7. **Type safety & ergonomics** — `Result<TValue, TError>` return type for every resource method; `Option<T>` for nullable model fields; no exception-as-flow-control for recoverable HTTP statuses.
8. **Forward compatibility** — `ApiVersion` header pinning lets host apps remain stable as Polar evolves their schema.
9. **First-class localization** — host app developers, end users, and operators can experience the SDK in `en-US` and `es-MX` out of the box, with extensible `IPolarLocalizer` for additional languages.
10. **Zero-config defaults** — `services.AddPolarInfrastructure(builder.Configuration)` with a sandbox token in `appsettings.json` is the entire integration for simple cases.

### 3.2 Secondary goals (P1)

11. **Real-time UI notifications** via `IPolarToastChannel` — webhook events drive Blazor/SignalR/SSE notification streams.
12. **Webhook reconciliation** — periodic replay of missed events via `polar.Events.ListAsync(since:)` to defend against silent webhook loss.
13. **dotnet new templates** — one command per webhook handler boilerplate.
14. **Customer Portal isolation** — separate `HttpClient` and Customer Access Token surface that cannot leak Organization Access Tokens.
15. **Health checks** — `IHealthCheck` integration so PolarSharp shows up in `/health` reports.

### 3.3 Tertiary goals (P2)

16. **In-memory webhook idempotency cache** — convenience for hosts without a distributed cache.
17. **JIT warmup** — opt-in startup ping that pre-compiles hot paths for the first user-facing request.
18. **Doxygen support** as secondary documentation toolchain for consumers who prefer it.

### 3.4 Non-goals (explicit)

- **Persistence** — PolarSharp owns no database, no EF Core, no Redis. Host apps manage their own state.
- **UI components** — PolarSharp ships no Blazor/Razor components. The host integrates `IPolarToastChannel` with their UI framework of choice.
- **Domain modelling beyond Polar's schema** — PolarSharp does not impose product, order, or pricing models. It exposes Polar's API verbatim (typed via Kiota).
- **OAuth flows for end-user login** — out of scope for v1; Polar's Customer Access Tokens cover Customer Portal use cases.
- **Cryptocurrency or non-Polar payment routing** — single provider scope.
- **API gateway / proxy server functionality** — PolarSharp is a client SDK, not a server.
- **Auto-generated tests** — Kiota generates HTTP requests; tests for that surface are integration tests against Polar's sandbox, not synthetic mocks.
- **Distributed tracing backends** — PolarSharp emits via `ActivitySource`; the host app chooses OTel exporters (Jaeger, Datadog, Honeycomb, etc.).

---

## 4. Target Audience & Personas

### Persona A — "Backend .NET Developer" (Primary)

- **Role:** Mid-to-senior software engineer building the server tier of a .NET web/API application.
- **Goals:** Integrate Polar payment processing with minimum boilerplate; type safety on request/response shapes; clear error semantics.
- **Pain points:** Polar has no .NET SDK; rolling-their-own takes a week and lacks enterprise rigor.
- **Success criteria:** First successful API call within 15 minutes of `dotnet add package PolarSharp`. Webhook handler scaffolded and verified end-to-end within 30 minutes.

### Persona B — "Enterprise Architect" (Secondary)

- **Role:** Architect responsible for cross-cutting concerns: observability, security, supply chain, compliance.
- **Goals:** Validate that PolarSharp meets internal standards before approving its use in production. Verify code is signed, deterministic, AOT-safe, has CVE scanning, follows SemVer strictly.
- **Pain points:** Many open-source SDKs lack signing, snapshot tests, or reproducible builds. Procurement reviews fail.
- **Success criteria:** Can answer every checklist item from internal security review using PolarSharp documentation alone.

### Persona C — "DevOps / SRE Engineer" (Secondary)

- **Role:** Owns deployment, monitoring, alerting, on-call response.
- **Goals:** Visibility into Polar API latency, error rates, webhook verification failures; alert on circuit-breaker open events; correlate Polar request IDs across logs.
- **Pain points:** Most SDKs emit no metrics or logs; tracking Polar-related issues requires custom instrumentation.
- **Success criteria:** PolarSharp metrics flow into existing Prometheus/Datadog setup with zero PolarSharp-specific configuration. Health check endpoint returns `Healthy`/`Degraded`/`Unhealthy` semantically.

### Persona D — "Compliance / Security Officer" (Tertiary)

- **Role:** GDPR/SOC2/PCI compliance review.
- **Goals:** Confirm that customer PII is not retained in logs, that webhook secrets are not exposed, that token rotation is supported, that data flows are documented.
- **Pain points:** Most SDKs log raw bearer tokens or full request bodies, creating GDPR liability.
- **Success criteria:** PolarSharp's `security.md` article answers every question on the internal compliance checklist.

---

## 5. User Stories & Use Cases

### UC-1 — Greenfield SaaS adoption

> *As a backend developer at a new SaaS startup, I want to add Polar payment processing to my .NET API in under 30 minutes so I can demo end-to-end checkout to my CEO this afternoon.*

**Steps:** `dotnet add package PolarSharp` → set `PolarSharp:AccessToken` in `appsettings.Development.json` via user-secrets → call `services.AddPolarInfrastructure(Configuration)` → inject `PolarClient` → call `polar.Checkouts.CreateAsync(...)`. **Done.**

### UC-2 — Multi-tenant B2B platform

> *As an architect at a B2B platform that hosts 200 customer organizations, each with their own Polar account, I want every customer's API calls and circuit-breaker state to be fully isolated from every other customer.*

**Solution:** `services.AddPolarInfrastructure(...).AddPolarMultiTenant()` with `Finbuckle.MultiTenant` resolution from header/route/hostname/claim. Per-tenant `PolarClient` instances cached via `LazyConcurrentDictionary`; per-tenant circuit breakers via `ResiliencePipelineRegistry<string>`.

### UC-3 — Webhook-driven order fulfillment

> *As a developer, I want to fulfill an order in my database the moment Polar confirms payment, with full at-least-once guarantees and replay safety.*

**Solution:** Register `OrderPaidHandler : PolarWebhookHandlerBase<OrderPaidEvent>` → implement idempotent fulfillment in `HandleCoreAsync` → register via `.AddWebhookHandler<OrderPaidEvent, OrderPaidHandler>()`. Optional `enqueue: true` flag wraps in `IBackgroundPolarWebhookQueue<T>` for handlers that exceed Polar's 30-second response timeout.

### UC-4 — AOT-compiled minimal API microservice

> *As a developer deploying to Azure Container Apps with cold-start sensitivity, I need a Polar SDK that works under Native AOT without manual trimming hints.*

**Solution:** PolarSharp ships with `IsAotCompatible=true` and `IsTrimmable=true`. CI gates `dotnet publish -p:PublishAot=true` with zero warnings. Static-list `KnownWebhookEventTypes.All`, source-gen STJ context, explicit `IValidateOptions<>`, no `Assembly.GetTypes()`/reflection.

### UC-5 — Real-time admin dashboard

> *As a product manager, I want our internal Blazor admin app to show toast notifications the instant a new high-value subscription is created — for the operator's locale (English or Spanish).*

**Solution:** Configure `PolarSharp:Webhooks:ToastNotifications:Events` in `appsettings.json` → register `.AddPolarToastNotifications()` → consume `IPolarToastChannel.Reader` in a Blazor `@implements IAsyncDisposable` layout component → call `toast.Localize(localizer)` at render time so each user sees their browser-locale-correct version.

### UC-6 — Live ↔ test mode switching

> *As a release engineer, I need a foolproof way to ensure staging hits Polar's sandbox while production hits Polar's live environment, with a loud warning if the wrong token is used in the wrong environment.*

**Solution:** `PolarSharp:Mode = "Test" | "Live" | "Custom"`. Startup banner shouts on Live mode. Token-prefix sanity check (`tok_sandbox_` vs `tok_live_`) emits a `Warning` if mismatched.

### UC-7 — API version pinning

> *As a senior developer, I want my host app to be insulated from Polar's API schema changes — even if Polar adds new fields or deprecates endpoints, my deployed code should keep behaving identically.*

**Solution:** Set `PolarSharp:ApiVersion: "2025-01-15"` (ISO date). PolarSharp sends `Polar-Version: 2025-01-15` on every request. STJ ignores unknown response fields. Set `ApiVersionStrictness: "Strict"` in regulated environments to fail startup on any drift from the SDK's bundled version.

### UC-8 — Customer Portal embedding

> *As a developer, I want to give my customers a self-service portal where they can manage their own subscriptions and download invoices, without giving them organization-level access to my Polar account.*

**Solution:** Server-side: call `polar.CustomerSessions.CreateAsync(new { CustomerId })` to mint a Customer Access Token (limited scope). Use `PolarCustomerPortalClient` — separate named `HttpClient` (`"PolarSharp.CustomerPortal"`) — to interact with `/v1/customer-portal/*` endpoints. The OAT and customer tokens never share a `HttpClient`, which is a hard security boundary.

### UC-9 — Webhook reconciliation after outage

> *As an SRE, after a 30-minute network outage between our app and Polar, I need to backfill any webhook events Polar tried to deliver and gave up on.*

**Solution:** `services.AddPolarWebhookReconciliation(opts => { opts.IntervalMinutes = 15; opts.MaxLookbackHours = 24; })`. The `IHostedService` checkpoints `lastProcessedTimestamp` and replays missed events via `polar.Events.ListAsync(since:)` through the same `IPolarWebhookDispatcher` that handles live webhooks. Idempotent handlers (already required) make replays safe.

### UC-10 — Token rotation in production

> *As a security engineer, I rotate Polar access tokens every 90 days. I cannot tolerate a deployment to apply the new token — that would interrupt customer transactions.*

**Solution:** `BearerTokenHandler` uses `IOptionsMonitor<PolarOptions>`. Update `appsettings.json` (or Azure Key Vault, AWS Secrets Manager via `IConfiguration`) → next outbound request uses the new token. Zero restart. Token-prefix sanity check re-runs on hot-reload.

---

## 6. Functional Requirements

Each requirement below is tagged with priority (P0 = must-have for v1.0; P1 = should-have for v1.x; P2 = nice-to-have for future). Every requirement maps to specific implementation in the corresponding plan phase.

### 6.1 Core HTTP client (P0 — Phase 3)

| ID | Requirement |
|---|---|
| FR-001 | Provide `PolarClient` as the public entry point, registered as Singleton in DI. |
| FR-002 | Expose typed resource properties for all 25+ Polar resource areas (Orders, Subscriptions, Customers, Products, Checkouts, CheckoutLinks, Benefits, BenefitGrants, Discounts, Refunds, LicenseKeys, Meters, CustomerMeters, Events, CustomerSeats, CustomerSessions, Webhooks, Organizations, Members, Files, CustomFields, OAuth2). |
| FR-003 | Every resource method accepts `CancellationToken ct = default`. |
| FR-004 | Every resource method returns `Task<Result<TValue, PolarError>>` — never throws on 4xx HTTP responses. |
| FR-005 | Underlying transport is generated by Microsoft Kiota from `https://api.polar.sh/openapi.json` with the spec snapshot pinned via `kiota.lock`. |

### 6.2 Authentication & token management (P0 — Phase 3)

| ID | Requirement |
|---|---|
| FR-010 | Inject `Authorization: Bearer <token>` on every outbound request via `BearerTokenHandler`. |
| FR-011 | Read token from `IOptionsMonitor<PolarOptions>` so `appsettings.json` hot-reload triggers zero-downtime token rotation. |
| FR-012 | Never log the raw token in any log entry; redact via log scope. |
| FR-013 | Validate token presence at startup via `IValidateOptions<PolarOptions>` — startup fails with clear message if missing. |
| FR-014 | Detect token-prefix vs mode mismatch (`tok_sandbox_` with `Mode=Live` or vice versa) and emit a startup `Warning`. |

### 6.3 Resilience & retry (P0 — Phase 3)

| ID | Requirement |
|---|---|
| FR-020 | Use `Microsoft.Extensions.Http.Resilience` to add retry + circuit breaker + timeout to the named `HttpClient`. |
| FR-021 | Retry on HTTP 429, 500, 502, 503, 504. Default max retries: 3. Configurable via `PolarSharp:MaxRetries` (range 0–10). |
| FR-022 | Exponential backoff with jitter; default 200ms base, doubled per attempt, +/- 30% jitter. |
| FR-023 | On 429, read `Retry-After` response header and wait exactly that duration before retrying (instead of backoff schedule). |
| FR-024 | Circuit breaker: opens after 5 consecutive failures within 30s; half-open probe after 15s. Configurable via `PolarSharp:Resilience`. |
| FR-025 | Per-attempt timeout: `PolarOptions.TimeoutMs` (default 30s, range 1s–5m). |
| FR-026 | Optional hedging strategy (`PolarSharp:Resilience:HedgeAfterMs`) — duplicate request after N ms for `GET`/`HEAD` only; mutating verbs explicitly excluded. |
| FR-027 | Circuit-breaker state transitions emit structured `Warning` logs. |

### 6.4 Idempotency keys (P0 — Phase 3)

| ID | Requirement |
|---|---|
| FR-030 | Auto-generate `X-Idempotency-Key` (Guid v7-based) on every mutating request (`POST`, `PATCH`, `DELETE`). |
| FR-031 | Reuse the same idempotency key across all retries of the same logical request — never regenerate per attempt. |
| FR-032 | Allow caller to supply a custom key via `PolarRequestOptions.IdempotencyKey`. |
| FR-033 | Never attach an idempotency key to `GET`/`HEAD` requests. |
| FR-034 | Log idempotency keys at `Debug` level for retry correlation. |

### 6.5 API versioning (P0 — Phase 3)

| ID | Requirement |
|---|---|
| FR-040 | Send `Polar-Version: <ISO-date>` header on every outbound request via `ApiVersionHandler`. |
| FR-041 | Default version is `PolarApiMetadata.GeneratedAgainstVersion` — a compile-time const set by the Kiota regen script. |
| FR-042 | Support override via `PolarSharp:ApiVersion` (ISO date `YYYY-MM-DD` only; non-date strings fail startup validation). |
| FR-043 | `ApiVersionStrictness` enum: `Warn` (default — log mismatch but continue), `Strict` (fail startup on mismatch), `Off` (silent). |
| FR-044 | Detect newer-than-SDK and older-than-SDK version mismatches separately and log accordingly. |
| FR-045 | Support URL path version override via `PolarSharp:BasePath` (default `/v1`); must start with `/`. |
| FR-046 | Hot-reload of `ApiVersion` via `IOptionsMonitor` — next outbound request uses new value. |
| FR-047 | STJ deserialization must ignore unknown JSON properties (default behavior) so Polar-side field additions are non-breaking. |

### 6.6 Webhooks (P0 — Phase 4 + 4c)

| ID | Requirement |
|---|---|
| FR-050 | Verify webhook HMAC-SHA256 signature per Standard Webhooks spec (`webhook-id`, `webhook-timestamp`, `webhook-signature` headers). |
| FR-051 | Validate webhook timestamp within ±5 minutes (configurable `ToleranceSeconds`) to prevent replay attacks. |
| FR-052 | Constant-time signature comparison via `CryptographicOperations.FixedTimeEquals`. |
| FR-053 | Support **multi-secret rotation** — `PolarSharp:Webhooks:Secrets` is a list; verification passes if any configured secret matches. |
| FR-054 | Backwards-compat shorthand: single-string `Secret` parsed as one-element list. |
| FR-055 | Strongly-typed event records: `WebhookEvent` abstract base, `sealed record` per event type, custom `JsonConverter` for discriminator-based deserialization. |
| FR-056 | `IPolarWebhookHandler<TEvent>` interface; developer implements one per event type they handle. |
| FR-057 | `PolarWebhookHandlerBase<TEvent>` abstract class — seals `HandleAsync`, exposes `HandleCoreAsync`. |
| FR-058 | `.AddWebhookHandler<TEvent, THandler>()` fluent registration (Scoped DI lifetime per handler). |
| FR-059 | `IPolarWebhookDispatcher` (internal) routes verified events to registered handlers within request DI scope. |
| FR-060 | Unhandled event type → `Warning` log; does not throw. |
| FR-061 | `PolarWebhookStartupValidator` (`IHostedService`) runs handler completeness check at startup against `KnownWebhookEventTypes.All` static list. |
| FR-062 | `FailOnMissingHandlers = true` config flag fails startup if any known event type has no handler. |
| FR-063 | Optional `enqueue: true` flag on `AddWebhookHandler<T>` — wraps in `IBackgroundPolarWebhookQueue<T>` (bounded `Channel<T>`) + `IHostedService` for slow handlers. |
| FR-064 | Background queue drains on graceful shutdown with configurable timeout (default 30s). |
| FR-065 | MVC integration: `[ValidatePolarWebhook]` action filter for controller-based handlers. |
| FR-066 | Cancellation token passed to handler is bounded — wraps HTTP request token with non-cancellable scope so HTTP disconnect does not abort fulfillment mid-execution. |
| FR-067 | Idempotency requirement (at-least-once delivery from Polar) documented in `HandleCoreAsync` XML `<remarks>`; `WebhookId` exposed for dedup. |

### 6.6.1 Webhook security (P0 — Phase 4c)

| ID | Requirement |
|---|---|
| FR-070 | Payload size cap: `MaxPayloadBytes = 1 MB` default; returns 413 before body read. |
| FR-071 | Rate limiting: ASP.NET Core fixed-window limiter, 300 req/min per IP default; returns 429 with hashed-IP metric. |
| FR-072 | Optional IP allowlisting (`AllowedSourceIpRanges`); checks `RemoteIpAddress` before body read; returns 403. |
| FR-073 | `Content-Type` enforcement: only `application/json` accepted; returns 415. |
| FR-074 | HTTPS-only enforcement: returns 400 (NOT redirect) for non-HTTPS, since Polar sender doesn't follow redirects. |
| FR-075 | Timing-uniform error responses: same status, body, and response time for every failure mode (HMAC computed even on bad timestamp). |
| FR-076 | JSON deserialization hardening: `MaxDepth=32`, `AllowTrailingCommas=false`, `ReadCommentHandling=Disallow`. |
| FR-077 | Anomaly detection: `Warning` log when verification failure rate from one IP exceeds 10/60s; `polar.webhooks.suspicious_activity` gauge. |
| FR-078 | IP addresses SHA-256 hashed before logging (GDPR/privacy). |
| FR-079 | Webhook path obscurity: startup `Warning` on default path; deterministic GUID-based path suggestion. |

### 6.7 Multi-tenancy (P1 — Phase 5 + 11)

| ID | Requirement |
|---|---|
| FR-090 | `PolarTenantInfo : ITenantInfo` (Finbuckle) extended with `PolarAccessToken` and `Server` per tenant. |
| FR-091 | Resolve tenant via Finbuckle strategies: `Header`, `Route`, `Hostname`, `Claim`. |
| FR-092 | Configure tenants from `PolarSharp:MultiTenant:Tenants` array in `appsettings.json` OR from `IOptions<PolarMultiTenantOptions>` programmatic registration. |
| FR-093 | `IMultiTenantPolarClientFactory.GetClientForCurrentTenant()` returns a `PolarClient` scoped to the current Finbuckle tenant context. |
| FR-094 | **Per-tenant bulkhead isolation**: each tenant gets own named `HttpClient`, `SocketsHttpHandler`, circuit breaker, rate limiter via `ResiliencePipelineRegistry<string>` keyed `PolarSharp.Tenant.{id}`. |
| FR-095 | Race-free per-tenant client creation: `LazyConcurrentDictionary<TenantId, PolarClient>` guarantees factory delegate runs at most once per tenant under contention. |
| FR-096 | All cached per-tenant clients disposed in parallel on graceful shutdown via `IAsyncDisposable`. |

### 6.8 Toast notifications (P1 — Phase 4b)

| ID | Requirement |
|---|---|
| FR-100 | `IPolarToastChannel` exposes `ChannelReader<PolarToastNotification>` (bounded; default capacity 100). |
| FR-101 | Whitelist of toast-emitting event types via `PolarSharp:Webhooks:ToastNotifications:Events` array. |
| FR-102 | Each whitelist entry: `EventType`, `Title`, `MessageTemplate`, `Severity`, `DurationSeconds`. |
| FR-103 | `{Token}` placeholder substitution in `MessageTemplate` via static AOT-safe property extractors per event type. |
| FR-104 | `PolarToastNotification` record carries 25+ typed fields (order, customer, subscription, pricing, error context) plus `ExtendedProperties` for forward compatibility. |
| FR-105 | **Lazy localization**: record carries `TitleLocalizationKey`, `MessageLocalizationKey`, `TokenValues`; consumer calls `Localize(IPolarLocalizer)` at UI render time. |
| FR-106 | Pre-rendered en-US fallback always present in `Title`/`Message` — apps that never call `Localize()` still display correct English. |
| FR-107 | Channel back-pressure: `BoundedChannelFullMode.DropOldest`; full channel logs `Debug`, business event already handled by webhook handler. |
| FR-108 | `polar.channel.depth` gauge per channel name for production monitoring. |

### 6.9 Localization (P1 — Phase 6)

| ID | Requirement |
|---|---|
| FR-110 | Public `IPolarLocalizer` interface; library registers built-in `PolarResourceLocalizer` via `TryAddSingleton` only if host has not registered a custom impl. |
| FR-111 | Built-in support for `en-US` (neutral default) and `es-MX` — both `.resx` files 100% complete; CI test enforces no missing keys. |
| FR-112 | All user-facing error messages, webhook errors, multi-tenant errors localized via `PolarMessageKeys` constants. |
| FR-113 | Toast titles & message templates localized via `PolarWebhookMessageKeys` (15 event types × 2 keys = 30 entries). |
| FR-114 | Resolution order: `appsettings.json` template override → host `IPolarLocalizer` → library `.resx` → pre-rendered en-US fallback. |
| FR-115 | `UseRequestLocalization` registered by `UsePolarInfrastructure` only if host hasn't already registered it. |

### 6.10 Customer Portal (P1 — Phase 3)

| ID | Requirement |
|---|---|
| FR-120 | `PolarCustomerPortalClient` exposed only on `polar.CreateCustomerPortalClient(customerToken)`. |
| FR-121 | Separate named `HttpClient` (`"PolarSharp.CustomerPortal"`) — never shares `HttpClient` with OAT-authenticated client. |
| FR-122 | Surface limited to `/v1/customer-portal/*` endpoints. |
| FR-123 | Same `ApiVersionHandler` and `BearerTokenHandler` pattern, but with Customer Access Token. |

### 6.11 Webhook reconciliation (P1 — Phase 12)

| ID | Requirement |
|---|---|
| FR-130 | Opt-in `PolarWebhookReconciler` `IHostedService` via `services.AddPolarWebhookReconciliation(...)`. |
| FR-131 | Periodically calls `polar.Events.ListAsync(since: lastProcessedTimestamp)` and dispatches missed events through `IPolarWebhookDispatcher`. |
| FR-132 | Pluggable checkpoint storage: `File`, `Redis`, `SqlServer`, `PostgreSQL`, `Custom` (via `IReconciliationCheckpointStore`). |
| FR-133 | Configurable `IntervalMinutes` (default 15) and `MaxLookbackHours` (default 24). |
| FR-134 | Replays use the same handler pipeline; idempotent handlers make replay safe. |

### 6.12 Templates (P2 — Phase 4c)

| ID | Requirement |
|---|---|
| FR-140 | `PolarSharp.Templates` NuGet pack with `dotnet new polar-handler --event <EventType>` template. |
| FR-141 | Generated handler is compilable, XML-documented, lists available event data properties for the chosen event type. |
| FR-142 | Template covers all 28 known Polar webhook event types via conditional `template.json` symbols. |

### 6.13 Health & observability (P0/P1 — Phase 3 + 11)

| ID | Requirement |
|---|---|
| FR-150 | `PolarHealthCheck : IHealthCheck` calls `GET /v1/organizations?limit=1`; returns `Healthy`/`Degraded`/`Unhealthy` with tag `"polar"`. |
| FR-151 | `ActivitySource("PolarSharp", version)` per outbound API call with tags `polar.resource`, `polar.operation`, `http.status_code`, `polar.request_id`, `error.type`. |
| FR-152 | `Meter("PolarSharp", version)` exposing `polar.requests`, `polar.errors`, `polar.request.duration`, `polar.requests.inflight`, `polar.webhooks.received`, `polar.webhooks.verification_failures`, `polar.webhooks.rejected_*`, `polar.channel.depth`. |
| FR-153 | All metrics tagged with `polar.tenant_id` (when multi-tenant), `polar.resource`, `polar.operation`. |
| FR-154 | Structured `ILogger.BeginScope()` on every API call with full Polar correlation context. |
| FR-155 | Automatic PII redaction in log scopes via `PolarPiiRedactor` (configurable; on by default in production). |

---

## 7. Non-Functional Requirements

### 7.1 Performance

| ID | Requirement | Target |
|---|---|---|
| NFR-001 | Per-call overhead vs raw `HttpClient` | < 5 ms (P50) |
| NFR-002 | Webhook signature verification latency | < 2 ms for 50 KB payload (P99) |
| NFR-003 | JSON serialization (per response) | < 1 ms for typical Order response (P99) |
| NFR-004 | Multi-tenant client lookup | < 100 ns (P99) — `LazyConcurrentDictionary` cache hit |
| NFR-005 | First-call latency after JIT warmup (or AOT) | < 50 ms beyond network RTT |
| NFR-006 | Memory allocation per webhook delivery | 0 GC-pressure allocations (after warmup) — `ArrayPool` for body |
| NFR-007 | Tail latency (P99) under hedging | ≥ 60% reduction vs no-hedging baseline for `GET` requests |

### 7.2 Reliability & availability

| ID | Requirement |
|---|---|
| NFR-010 | Survive Polar API outages of up to 15 seconds without surfacing errors to users (circuit breaker + retry budget). |
| NFR-011 | Survive transient (HTTP 429, 5xx) errors automatically; retry exhaustion produces typed `RateLimitError`/`ServerError`. |
| NFR-012 | Zero webhook loss during ≤ 24-hour outages when reconciliation is enabled. |
| NFR-013 | Zero-downtime token rotation (no app restart required). |
| NFR-014 | Zero-downtime webhook secret rotation (multi-secret support). |
| NFR-015 | Graceful shutdown drains in-flight requests + background queues within 30s; logs warning if exceeded. |

### 7.3 Security

| ID | Requirement |
|---|---|
| NFR-020 | All outbound TLS 1.2+ only (`SslProtocols.Tls12 \| Tls13`). |
| NFR-021 | Certificate revocation checked (`CheckCertificateRevocationList = true`). |
| NFR-022 | No automatic redirect following (SSRF defense). |
| NFR-023 | `CustomBaseUrl` blocked from RFC 1918 / metadata-endpoint hosts at startup. |
| NFR-024 | Webhook HMAC verification constant-time. |
| NFR-025 | Bearer tokens, webhook secrets, and full request/response bodies never logged. |
| NFR-026 | PII redaction in log scopes (configurable; default on in production). |
| NFR-027 | Strong-named, code-signed assemblies. |
| NFR-028 | NuGet packages NuGet-signed. |
| NFR-029 | Reproducible deterministic builds. |
| NFR-030 | CI vulnerability scan via `dotnet list package --vulnerable --include-transitive` + CodeQL + Dependabot. |

### 7.4 Scalability & concurrency

| ID | Requirement |
|---|---|
| NFR-040 | `PolarClient` thread-safe; safe to call concurrently from any number of threads. |
| NFR-041 | Per-tenant bulkhead: one tenant's circuit-broken state has zero effect on other tenants' latency. |
| NFR-042 | Linear scaling tested up to `MaxConnectionsPerServer` connections (default 100). |
| NFR-043 | Race-free per-key initialization proven via property-based tests under 1000-thread contention. |
| NFR-044 | HTTP/2 multiplexing enabled by default; one TCP connection serves hundreds of concurrent requests. |
| NFR-045 | DNS rotation refreshed every 15 min via `PooledConnectionLifetime`. |

### 7.5 Observability

| ID | Requirement |
|---|---|
| NFR-050 | Every outbound API call traced via `ActivitySource` with full attribute set. |
| NFR-051 | Every outbound API call metered (count, duration histogram, error rate). |
| NFR-052 | Inflight gauge per tenant + resource for capacity planning. |
| NFR-053 | Structured logs with full Polar correlation context (request ID, tenant ID, resource, operation). |
| NFR-054 | Health check integrates with standard `IHealthCheck` infrastructure. |
| NFR-055 | Zero overhead when no `ActivityListener` / `MeterListener` is attached. |

### 7.6 Maintainability

| ID | Requirement |
|---|---|
| NFR-060 | All hand-written `.cs` ZH-compliant: value objects, immutable records, monads for recoverable errors, polymorphism over branching, SLAP. |
| NFR-061 | XML doc comments on every public/protected member; `WarningsAsErrors=CS1591`. |
| NFR-062 | Public API surface snapshot tested per release. |
| NFR-063 | Wire-format JSON snapshot tested per public record. |
| NFR-064 | `ConfigureAwait(false)` enforced library-wide via `VSTHRD111` analyzer. |
| NFR-065 | Strict SemVer; `CHANGELOG.md` updated on every PR that touches `src/`. |
| NFR-066 | Kiota client regenerable from spec via documented script; `kiota.lock` committed. |

### 7.7 Compatibility & portability

| ID | Requirement |
|---|---|
| NFR-070 | Targets `net10.0` only — no compat shims for older frameworks. |
| NFR-071 | `IsAotCompatible=true` and `IsTrimmable=true` on all library projects; CI gates `dotnet publish -p:PublishAot=true` with zero warnings. |
| NFR-072 | Works on Windows, Linux, macOS (no platform-specific code in libraries). |
| NFR-073 | Works in Blazor Server, Minimal API, MVC, console apps, BackgroundService, IHostedService, Azure Functions isolated worker. |
| NFR-074 | No dependency on legacy `HttpClientHandler` — uses `SocketsHttpHandler` directly. |

### 7.8 Compliance & data privacy

| ID | Requirement |
|---|---|
| NFR-080 | GDPR Article 5 (data minimization) — `PolarPiiRedactor` masks customer emails/names/error details in logs by default in production. |
| NFR-081 | GDPR Article 32 (security of processing) — TLS 1.2+, no plaintext credentials, no raw IPs in logs. |
| NFR-082 | PCI DSS — PolarSharp never receives, stores, or logs cardholder data; Polar handles all card flows. |
| NFR-083 | SOC 2 — supports the host app's audit posture: complete observability, structured logs, signed assemblies, reproducible builds. |
| NFR-084 | SLSA Level 3 — deterministic builds, source-link attestation, NuGet package signing. |
| NFR-085 | Standard Webhooks spec compliance for webhook signature verification. |

### 7.9 AOT compatibility (cross-cutting)

See **AOT Compliance Checklist** section in the main plan. All requirements are NFR-equivalent gates enforced by `dotnet publish -p:PublishAot=true` in CI with zero warnings.

---

## 8. Technical Architecture

### 8.1 Three-package layout

```
PolarSharp                 (NuGet: PolarSharp)
  ├─ PolarClient + 25 resource wrappers
  ├─ Auth, Resilience, Idempotency, Versioning, Connection, Concurrency
  ├─ Telemetry (ActivitySource + Meter)
  ├─ Health check
  ├─ CustomerPortal client
  ├─ Pagination helpers
  ├─ Result<T,E>, Option<T>, PolarError hierarchy
  └─ Localization (IPolarLocalizer + en-US/es-MX .resx)

PolarSharp.Webhooks         (NuGet: PolarSharp.Webhooks)
  ├─ WebhookValidator (HMAC-SHA256, multi-secret rotation)
  ├─ WebhookEvent + 28 sealed event records
  ├─ IPolarWebhookHandler<TEvent> + base class + dispatcher
  ├─ Startup completeness validator
  ├─ Background queue adapter
  ├─ Toast notifications (IPolarToastChannel)
  ├─ Reconciliation IHostedService
  └─ Webhook-specific localization

PolarSharp.MultiTenant      (NuGet: PolarSharp.MultiTenant)
  ├─ Finbuckle.MultiTenant integration
  ├─ PolarTenantInfo : ITenantInfo
  ├─ IMultiTenantPolarClientFactory
  ├─ Per-tenant bulkhead via ResiliencePipelineRegistry<string>
  └─ Multi-tenant-specific localization

PolarSharp.Templates        (NuGet: PolarSharp.Templates)
  └─ dotnet new polar-handler template pack
```

### 8.2 Outbound HTTP handler pipeline

```
Caller (PolarClient.Orders.GetAsync)
   │
   ▼
Kiota request builder
   │
   ▼
Named HttpClient ("PolarSharp" or "PolarSharp.Tenant.{id}")
   │
   ├── BearerTokenHandler        (injects Authorization, scrubbed from logs)
   ├── IdempotencyKeyHandler     (X-Idempotency-Key on POST/PATCH/DELETE)
   ├── ApiVersionHandler         (Polar-Version header)
   ├── ResilienceHandler         (retry + circuit breaker + timeout + optional hedging)
   │
   ▼
Primary handler: SocketsHttpHandler
   ├── PooledConnectionLifetime = 15min
   ├── PooledConnectionIdleTimeout = 2min
   ├── MaxConnectionsPerServer = 100
   ├── EnableMultipleHttp2Connections = true
   ├── AllowAutoRedirect = false (SSRF defense)
   └── SslOptions: TLS 1.2+, CRL check
```

### 8.3 Webhook ingestion pipeline

```
Polar webhook POST → /hooks/polar
   │
   ├── Rate limiter (300/min per IP)             ─── 429 if exceeded
   ├── PayloadSizeLimitFilter (1 MB)              ─── 413 if exceeded
   ├── IP allowlist (opt-in)                      ─── 403 if not allowlisted
   ├── HTTPS check                                ─── 400 if not HTTPS
   ├── Content-Type check (application/json)      ─── 415 if other
   │
   ▼
WebhookValidator (HMAC-SHA256, multi-secret, timing-uniform)
   │  (always computes HMAC even on bad timestamp)
   │
   ▼ Result<WebhookEvent, WebhookVerificationError>
   │
   ▼ on success: IPolarWebhookDispatcher (request DI scope)
   │
   ▼ resolve IPolarWebhookHandler<TEvent>
   │
   ├── if (enqueue=true): write to IBackgroundPolarWebhookQueue<TEvent>; return 200
   │     │
   │     ▼
   │   PolarWebhookBackgroundService<TEvent> drains the channel and calls HandleAsync
   │
   └── else: invoke HandleCoreAsync inline; return 200 on success, 500 on exception
   │
   ▼ AFTER handler: dispatcher writes PolarToastNotification to IPolarToastChannel
                    if event type matches whitelist (TryWrite, non-blocking)
```

### 8.4 Per-tenant bulkhead

```
Request arrives → Finbuckle resolves tenant from header/route/hostname/claim
   │
   ▼ IMultiTenantPolarClientFactory.GetClientForCurrentTenant()
   │
   ▼ LazyConcurrentDictionary<TenantId, PolarClient>.GetOrAdd(...)
   │  (Lazy<T> guarantees factory runs once per tenant under contention)
   │
   ▼ Per-tenant named HttpClient: "PolarSharp.Tenant.{tenantId}"
   │  (own SocketsHttpHandler, own connection pool)
   │
   ▼ Per-tenant ResiliencePipeline (keyed in ResiliencePipelineRegistry<string>)
   │  (own circuit breaker state, own rate limiter, own retry budget)
   │
   ▼ Per-tenant Bearer token (each tenant's own access token)
```

### 8.5 DI integration model

| Service type | Lifetime | Why |
|---|---|---|
| `IOptionsMonitor<PolarOptions>` | Singleton (BCL) | Hot-reload from `appsettings.json` |
| `PolarClient` | Singleton | Stateless wrapper over `IHttpClientFactory`-pooled HttpClient |
| `IPolarLocalizer` | Singleton | Stateless; reads `CultureInfo.CurrentUICulture` per indexer call |
| `IPolarToastChannel` | Singleton | One channel shared across all readers |
| `IMultiTenantPolarClientFactory` | Singleton | Shared cache of per-tenant clients |
| `IPolarWebhookHandler<TEvent>` | Scoped | Participates in request DI scope; can inject `DbContext`, `ICurrentUser` |
| `IPolarWebhookDispatcher` | Scoped | Resolves scoped handlers within the request scope |
| `PolarHealthCheck` | Scoped | Standard `IHealthCheck` lifetime |
| `PolarActivitySource` / `PolarMeter` | Singleton | Static `ActivitySource`/`Meter` instances |
| Background services (queue, reconciler, warmup) | Singleton (`IHostedService`) | Long-lived host-scoped processes |

---

## 9. Public API Surface (Summary)

This section enumerates the public surface that `PublicApiGenerator` snapshot tests will lock down at v1.0. Full XML doc comments accompany every member. Internal types are excluded.

### 9.1 Configuration

```
PolarSharp namespace:
- PolarOptions (class)
  - AccessToken : string
  - Mode : PolarMode { Test, Live, Custom }
  - Server : PolarServer { Production, Sandbox, Custom }
  - CustomBaseUrl : string?
  - BasePath : string ("/v1" default)
  - ApiVersion : string?
  - ApiVersionStrictness : ApiVersionStrictness { Warn, Strict, Off }
  - TimeoutMs : int (30_000 default)
  - MaxRetries : int (3 default)
  - Resilience : PolarResilienceOptions
  - Connection : PolarConnectionOptions
  - Logging : PolarLoggingOptions

- PolarResilienceOptions (class)
  - CircuitBreakerFailureThreshold, CircuitBreakerSamplingSeconds, CircuitBreakerBreakSeconds
  - HedgeAfterMs, HedgeMaxAttempts

- PolarConnectionOptions (class)
  - MaxConnectionsPerServer, PooledConnectionLifetimeMinutes, PooledConnectionIdleTimeoutMinutes
  - EnableHttp2, EnableHttp3, EnableMultipleHttp2Connections

- PolarLoggingOptions (class)
  - RedactPii : bool (true default)
  - RedactIdsToHashes : bool (false default)
```

### 9.2 Result/Option monads

```
PolarSharp namespace:
- Result<TValue, TError> (readonly record struct)
  - IsSuccess, Success(...), Failure(...)
  - Map<TResult>, Bind<TResult>, BindAsync<TResult>
  - Match<TResult>, MatchAsync<TResult>

- Option<T> (readonly record struct)
  - HasValue, Some(...), None
  - Map<TResult>, Bind<TResult>, Match<TResult>

- ResultExtensions (static class)
  - ToHttpResult<T>(...)  // Minimal API IResult conversion
```

### 9.3 Errors

```
PolarSharp namespace:
- PolarError (abstract record)
- AuthenticationError, AuthorizationError, NotFoundError,
  ValidationError, RateLimitError, ServerError,
  WebhookVerificationError (sealed records)
- FieldValidationError (sealed record)

Exceptions (unrecoverable infrastructure failures only):
- PolarNetworkException, PolarConfigurationException,
  PolarWebhookConfigurationException
```

### 9.4 Client surface

```
PolarSharp namespace:
- PolarClient (class)
  - Orders, Subscriptions, Customers, Products, Checkouts, CheckoutLinks,
    Benefits, BenefitGrants, Discounts, Refunds, LicenseKeys,
    Meters, CustomerMeters, Events, CustomerSeats, CustomerSessions,
    Webhooks, Organizations, Members, Files, CustomFields, OAuth2 (resource properties)
  - GeneratedAgainstVersion : string  (read-only diagnostic)
  - CreateCustomerPortalClient(customerToken : string) : PolarCustomerPortalClient

- PolarCustomerPortalClient (class)
  - Orders, Subscriptions, Benefits, Customers (limited resource properties)

- PaginatedList<T> (class)
- PaginationExtensions (static class)
  - ToAsyncEnumerable<T>(...)
```

### 9.5 DI extensions

```
PolarSharp.Extensions namespace:
- ServiceCollectionExtensions (static)
  - AddPolarInfrastructure(IServiceCollection, IConfiguration) : PolarInfrastructureBuilder

- ApplicationBuilderExtensions (static)
  - UsePolarInfrastructure(this IApplicationBuilder)

- PolarInfrastructureBuilder (class)
  - Services : IServiceCollection
  - Configuration : IConfiguration

PolarSharp.Webhooks namespace:
- WebhookBuilderExtensions (static)
  - AddPolarWebhooks(builder, configure?) : PolarInfrastructureBuilder
  - AddPolarToastNotifications(builder, configure?) : PolarInfrastructureBuilder
  - AddWebhookHandler<TEvent, THandler>(builder, enqueue=false) : PolarInfrastructureBuilder
  - AddPolarWebhookReconciliation(builder, configure?) : PolarInfrastructureBuilder
  - AddPolarWebhookInMemoryDedup(builder, configure?) : PolarInfrastructureBuilder

PolarSharp.MultiTenant namespace:
- MultiTenantBuilderExtensions (static)
  - AddPolarMultiTenant(builder, configure?) : PolarInfrastructureBuilder
```

### 9.6 Webhooks

```
PolarSharp.Webhooks namespace:
- WebhookEvent (abstract record)
- 28 sealed event records: OrderCreatedEvent, OrderPaidEvent, ..., RefundUpdatedEvent
- IPolarWebhookHandler<TEvent> (interface)
  - HandleAsync(TEvent, CancellationToken) : Task
- PolarWebhookHandlerBase<TEvent> (abstract class)
  - HandleAsync (sealed)
  - HandleCoreAsync (abstract — developer implements)
  - OnErrorAsync (virtual)
- PolarWebhookOptions (class)
- PolarToastNotification (sealed record) + PolarToastLineItem, PolarToastAmount, PolarToastError
- IPolarToastChannel (interface)
- ToastSeverity (enum)
- PolarToastOptions (class) + PolarToastEventConfig
```

### 9.7 Multi-tenant

```
PolarSharp.MultiTenant namespace:
- PolarTenantInfo : ITenantInfo (class)
- IMultiTenantPolarClientFactory (interface)
- PolarMultiTenantOptions (class)
- TenantStrategy (enum)
- HeaderStrategyOptions, RouteStrategyOptions, HostnameStrategyOptions, ClaimStrategyOptions (classes)
```

### 9.8 Localization

```
PolarSharp.Localization namespace:
- IPolarLocalizer (interface)
  - this[string key] : LocalizedString
  - this[string key, params object[] arguments] : LocalizedString
```

---

## 10. Data Models & Wire Format

All response/request DTOs are Kiota-generated under `PolarSharp.Generated` namespace and exposed via the resource property surface. Wire-format stability is enforced by `JsonSnapshots/*.json` Verify tests committed to source control.

PolarSharp follows the Polar OpenAPI spec verbatim — **PolarSharp does not add or rename fields**. Forward compatibility for new Polar fields is provided by STJ's default "ignore unknown properties" deserialization behavior.

---

## 11. Dependencies

### 11.1 Runtime dependencies (always required)

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Kiota.Abstractions` | 1.x | Kiota runtime |
| `Microsoft.Kiota.Http.HttpClientLibrary` | 1.x | Kiota HTTP transport |
| `Microsoft.Kiota.Serialization.Json` | 1.x | Kiota STJ adapter |
| `Microsoft.Kiota.Serialization.Text/Form/Multipart` | 1.x | Kiota content-type adapters |
| `Microsoft.Extensions.Http.Resilience` | 9.x | Retry + circuit breaker + hedging |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | 9.x | Health check infrastructure |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 9.x | `BindConfiguration` + `ValidateOnStart` |

### 11.2 Optional package dependencies

| Optional pkg | Adds | Purpose |
|---|---|---|
| `PolarSharp.Webhooks` | (no extra runtime deps beyond core) | Webhook handlers + toasts + reconciliation |
| `PolarSharp.MultiTenant` | `Finbuckle.MultiTenant.AspNetCore 9.x` | Multi-tenant integration |

### 11.3 Build-time / dev-time

| Package | Purpose |
|---|---|
| `Microsoft.SourceLink.GitHub` | SourceLink debugging |
| `Microsoft.VisualStudio.Threading.Analyzers` | `ConfigureAwait(false)` enforcement |
| `Microsoft.OpenApi.Kiota` (CLI tool) | Kiota client regeneration |
| `xunit` + `Microsoft.NET.Test.Sdk` | Tests |
| `BenchmarkDotNet` | Benchmarks |
| `Verify.Xunit` | Snapshot tests |
| `PublicApiGenerator` | Public API surface tests |
| `FsCheck` | Property-based tests |

---

## 12. Compliance & Standards Conformance

| Standard | Conformance level | Mechanism |
|---|---|---|
| **GDPR Article 5** (data minimization) | Compliant | `PolarPiiRedactor` + IP-hashing |
| **GDPR Article 32** (security of processing) | Compliant | TLS 1.2+, no plaintext credentials |
| **PCI DSS** | Out of scope | PolarSharp never receives cardholder data |
| **SOC 2 Type II** controls | Supports host posture | Observability, structured logs, signed builds |
| **SLSA Level 3** | Conformant | Deterministic builds, SourceLink, signed packages |
| **SemVer 2.0.0** | Strictly enforced | API snapshot tests + JSON snapshot tests + CHANGELOG.md gate |
| **Standard Webhooks v1.0** | Compliant | HMAC-SHA256, `webhook-id`/`-timestamp`/`-signature` headers, multi-secret |
| **OpenTelemetry semantic conventions** | Compliant | `ActivitySource` tags follow `http.*`, `error.type` conventions |
| **Common Changelog format** | Compliant | `CHANGELOG.md` machine-parseable |
| **NuGet best practices** | Compliant | Package metadata, README, icon, license, signing |

---

## 13. Operational Requirements

### 13.1 Deployment topologies supported

- ✅ ASP.NET Core Web API (Minimal API or MVC) on Kestrel.
- ✅ Blazor Server (with Telerik UI / MudBlazor toast integration).
- ✅ ASP.NET Core BlazorWebAssembly (server-rendered components only — webhooks require server).
- ✅ Console app + `Microsoft.Extensions.Hosting`.
- ✅ Azure Functions (isolated worker model only — in-proc not supported on net10).
- ✅ AWS Lambda .NET (with custom runtime).
- ✅ Azure Container Apps + Native AOT.
- ✅ Worker Service (`IHostedService` apps with no HTTP).

### 13.2 Health checks

- Endpoint: host-app-controlled (typically `/health`).
- PolarSharp registers `PolarHealthCheck` with tag `"polar"`.
- States: `Healthy` (200 from Polar), `Degraded` (429 from Polar), `Unhealthy` (any other failure).

### 13.3 Metrics & alerting (recommended host-app alerts)

| Metric | Recommended alert |
|---|---|
| `polar.requests.duration{status="error",p=99}` | > 5s for 5 min ⇒ page on-call |
| `polar.errors{type="ServerError"}` rate | > 1/sec sustained ⇒ page on-call |
| `polar.webhooks.verification_failures` rate | > 10/min sustained ⇒ security incident |
| `polar.webhooks.suspicious_activity` | == 1 ⇒ page on-call |
| `polar.requests.inflight{tenant_id="X"}` | > 80% of `MaxConnectionsPerServer` ⇒ warn |
| `polar.channel.depth{name="webhook-queue"}` | > 80% of `ChannelCapacity` ⇒ warn |
| Health check `/health` | `Unhealthy` for 3 consecutive checks ⇒ page on-call |

### 13.4 Logging & log aggregation

- All logs structured via `ILogger.BeginScope(IDictionary<string,object?>)`.
- Compatible with Serilog, NLog, Datadog, Splunk, Azure Monitor, AWS CloudWatch, Seq.
- `PolarPiiRedactor` masks PII before log entry leaves the process (configurable).

### 13.5 Distributed tracing

- `ActivitySource("PolarSharp", version)` exports via standard `ActivitySource` listener.
- Compatible with OpenTelemetry Collector, Jaeger, Zipkin, Datadog APM, Honeycomb, AWS X-Ray.
- Trace context propagated to Polar via `traceparent` (handled automatically by `HttpClient`).

### 13.6 Graceful shutdown

- Triggered by `IHostApplicationLifetime.ApplicationStopping`.
- Order: background queues drain (30s cap) → multi-tenant clients dispose in parallel → toast channel completes → SocketsHttpHandler instances dispose last (lets in-flight HTTP complete).

### 13.7 Token rotation procedure

1. Update `PolarSharp:AccessToken` in config source (Key Vault, Secrets Manager, etc.).
2. Config-reload triggers `IOptionsMonitor` callback.
3. Next outbound request uses new token. **No app restart required.**
4. Token-prefix sanity check re-runs on hot-reload; emits warning if new token mode-mismatches.

---

## 14. Success Metrics & KPIs

### 14.1 Adoption metrics (post-release)

- Downloads of `PolarSharp` from NuGet.org per month.
- GitHub stars, issues opened, PRs from external contributors.
- Number of NuGet packages depending on `PolarSharp` (consumed via NuGet API).

### 14.2 Performance metrics (CI-enforced)

- All NFR-001 to NFR-007 targets met in `PolarSharp.Benchmarks` runs.
- No benchmark regression > 10% above last release tag (CI-enforced gate).
- AOT publish: zero warnings (CI-enforced gate).

### 14.3 Quality metrics

- Code coverage: ≥ 85% for hand-written `src/` code (CI-tracked).
- All public/protected members documented (CS1591 = build error).
- Public API snapshot stable across all minor/patch releases (CI-enforced).
- JSON wire-format stable across all minor/patch releases (CI-enforced).

### 14.4 Operational metrics (host-app facing)

- P50 per-call overhead < 5ms.
- P99 webhook verification latency < 2ms.
- Zero webhook loss when reconciliation enabled (measured via test harness).

---

## 15. Quality Assurance Strategy

| Test type | Project | What it gates |
|---|---|---|
| Unit tests | `PolarSharp.Tests` | Core logic, monads, value objects, error mapping, localization completeness |
| Webhook unit tests | `PolarSharp.Webhooks.Tests` | HMAC verification, event parsing, dispatcher routing, completeness validator |
| Integration tests | `PolarSharp.IntegrationTests` | Live Polar sandbox API (skipped if `POLAR_SANDBOX_TOKEN` absent) |
| Property-based tests | `PolarSharp.Tests` (FsCheck) | `LazyConcurrentDictionary` race-freeness, `InflightTracker` net-zero, dedup-cache integrity |
| Benchmarks | `PolarSharp.Benchmarks` | Per-call overhead, allocation budgets, per-tenant isolation, hedging gain |
| API surface snapshots | `PolarSharp.Tests` (PublicApiGenerator + Verify) | Public API stability per release |
| Wire-format snapshots | `PolarSharp.Tests` (Verify) | JSON shape stability per public record |
| AOT smoke test | CI `dotnet publish -p:PublishAot=true` | Zero AOT warnings on PolarTestApp |
| Vulnerability scan | CI `dotnet list package --vulnerable` + CodeQL + Dependabot | Supply-chain security |

---

## 16. Documentation Strategy

- **DocFX** (primary) — generates GitHub Pages site from XML doc comments + Markdown articles. Auto-publishes on every push to `main` via `.github/workflows/docs.yml`.
- **Doxygen** (secondary) — opt-in for consumers preferring Doxygen XML/PDF output.
- **Per-package `README.md`** — visible on NuGet.org and IDE package explorers.
- **Conceptual articles** under `docs/articles/`:
  - `getting-started.md` — full annotated `Program.cs` template.
  - `configuration.md` — every `appsettings.json` field documented.
  - `webhooks.md`, `webhook-handlers.md` — handler patterns + completeness check.
  - `multi-tenancy.md` — Finbuckle setup, per-tenant bulkhead.
  - `localization.md` — adding new `.resx` files.
  - `pipeline-ordering.md` — middleware order with diagram + edge cases.
  - `test-vs-live-mode.md` — mode banner, token-prefix check.
  - `api-versioning.md` — version pinning, mismatch detection, regen workflow.
  - `toast-notifications.md` — `IPolarToastChannel`, lazy localization, integration patterns.
  - `security.md` — webhook hardening checklist, IP allowlist, token rotation, anomaly alerting.
  - `performance-tuning.md` — connection pool sizing, HTTP/2/3, hedging, benchmarks.
- **API reference** — auto-generated from XML doc comments by DocFX.

---

## 17. Release Plan & Phasing

### 17.1 v1.0.0 — Initial release scope

All P0 + P1 functional requirements in scope. v1.0 is feature-complete for production use. P2 items (in-memory dedup, JIT warmup, Doxygen) are also included since they are scoped & implemented in the plan.

Release gates:
1. All 12 implementation phases complete.
2. CI green: build, unit tests, property tests, AOT publish, vulnerability scan.
3. Public API + JSON snapshots committed and stable.
4. Documentation site published; DocFX articles complete.
5. Strong-named & code-signed artifacts produced; NuGet packages signed.

### 17.2 v1.x roadmap (post-launch)

- **v1.1** — Add `fr-FR` and `de-DE` localization (no breaking changes; additive `.resx` files).
- **v1.2** — Add observability presets for Datadog and Honeycomb (optional packages).
- **v1.3** — `PolarSharp.Reconciliation.Redis` and `.Sql` packages (concrete checkpoint store implementations).
- **v1.4** — Server-Sent Events helper package for toast notification streaming.

### 17.3 v2.0 considerations (12+ months out)

- Triggered by Polar releasing `/v2/` URL path version.
- Breaking change carrier: any unavoidable API surface adjustment forced by Polar's schema evolution.
- Released only under tagged migration guide.

---

## 18. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Polar OpenAPI spec drift breaks Kiota generation | Medium | High | `kiota.lock` pins spec snapshot; regeneration is a human-reviewed PR. |
| New Polar event types added without SDK update | High | Medium | `PolarWebhookStartupValidator` catches missing handlers at startup. STJ ignores unknown JSON properties. |
| Polar deprecates an endpoint we use | Low | Medium | `ApiVersionStrictness=Strict` mode + version mismatch detection at startup gives operators advance warning. |
| AOT compatibility regression in a future BCL release | Medium | High | CI gates `dotnet publish -p:PublishAot=true` with zero warnings on every PR. |
| Webhook secret leak via misconfiguration | Low | Critical | Multi-secret rotation, secret never logged, `PolarOptions.Webhooks.Secret` startup validation. |
| Per-tenant connection pool exhaustion | Medium | Medium | `polar.requests.inflight` gauge surfaces saturation; `MaxConnectionsPerServer` tunable. |
| Webhook loss during host-app outage | Medium | High | `PolarWebhookReconciler` opt-in service replays missed events. |
| Race condition in `MultiTenantPolarClientFactory` | Low | High | `LazyConcurrentDictionary` + property-based tests prove single-execution per key. |
| Memory leak from unscoped `IDisposable` | Low | Medium | `IAsyncDisposable` on factory; graceful shutdown ordering documented; tested. |
| GDPR violation via unredacted PII in logs | Medium | Critical | `PolarPiiRedactor` on by default in production; documented in `security.md`. |
| Supply-chain attack via compromised dependency | Low | Critical | CodeQL + Dependabot + `dotnet list package --vulnerable` in CI; all deps version-pinned in `Directory.Packages.props`. |
| Benchmark regression undetected | Low | Medium | CI fails any benchmark > 10% above last release tag. |
| Public API accidental breaking change | Medium | High | `PublicApiGenerator` snapshot tests + `CHANGELOG.md` gate on PR. |

---

## 19. Open Questions

| # | Question | Owner | Resolution path |
|---|---|---|---|
| OQ-1 | Polar's actual `Polar-Version` header name (assumed) — verify against current docs before release. | Eng lead | Empirical test against sandbox. |
| OQ-2 | Polar's webhook source IP ranges for opt-in allowlist — list documented? | Eng lead | Email Polar support. |
| OQ-3 | Polar's documented OpenAPI spec version field name (`info.x-polar-api-version` assumed) — confirm. | Eng lead | Inspect spec at `https://api.polar.sh/openapi.json`. |
| OQ-4 | Code-signing certificate provider (DigiCert vs Sectigo vs SignPath cloud) — procurement choice. | Owner | Internal procurement decision. |
| OQ-5 | NuGet package signing key — managed by GitHub Actions secrets vs. Azure Key Vault. | Eng lead | Operational decision before v1.0 ship. |
| OQ-6 | Public GitHub org name for repo URL — `polarsharp/polarsharp` or under owner's personal namespace? | Owner | Decide before publishing. |

---

## 20. Glossary

| Term | Definition |
|---|---|
| **AOT** | Ahead-Of-Time compilation — produces native machine code at build time, no JIT at runtime. |
| **Bulkhead** | Resilience pattern that isolates failures to prevent cascade across components. |
| **Hedging** | Sending duplicate requests after a delay to reduce tail latency. |
| **HMAC** | Hash-based Message Authentication Code — cryptographic primitive for verifying message integrity & origin. |
| **Idempotency key** | Client-supplied unique identifier that lets a server safely deduplicate retried requests. |
| **Kiota** | Microsoft's OpenAPI client generator producing AOT-friendly C# code. |
| **MOR** | Merchant of Record — a payment platform that becomes the legal seller (handles VAT, taxes, regulatory). |
| **OAT** | Organization Access Token — Polar's primary authentication method for server-side calls. |
| **SLAP** | Single Level of Abstraction Principle — every method body operates at one level of abstraction. |
| **SLSA** | Supply-chain Levels for Software Artifacts — Google-led specification for secure software supply chains. |
| **SSRF** | Server-Side Request Forgery — vulnerability where a server is tricked into making outbound requests to internal infrastructure. |
| **STJ** | `System.Text.Json` — .NET's first-party JSON serializer. |
| **ZH** | ZoranHorvat — refers to the coding rules in `~/ZoranHorvat.md`. |

---

## 21. References

1. Polar.sh API documentation — `https://docs.polar.sh`
2. Polar.sh OpenAPI spec — `https://api.polar.sh/openapi.json`
3. Polar Python SDK source — `https://github.com/polarsource/polar-python-sdk`
4. Standard Webhooks specification — `https://www.standardwebhooks.com/`
5. Microsoft Kiota documentation — `https://learn.microsoft.com/en-us/openapi/kiota/`
6. .NET Native AOT — `https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/`
7. `Microsoft.Extensions.Http.Resilience` — `https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience`
8. Finbuckle.MultiTenant — `https://www.finbuckle.com/MultiTenant`
9. ZoranHorvat coding rules — `~/ZoranHorvat.md` (project-internal)
10. SLSA specification — `https://slsa.dev/`
11. GDPR — `https://gdpr.eu/`
12. Common Changelog format — `https://common-changelog.org/`
13. SemVer 2.0.0 — `https://semver.org/`

---

## 22. Document Control

| Version | Date | Author | Notes |
|---|---|---|---|
| 1.0 | 2026-05-07 | Mark Chipman | Initial PRD derived verbatim from approved implementation plan. |

**Sign-off (required before development starts):**

- [ ] Product Owner: ___________________________ (date)
- [ ] Engineering Lead: ___________________________ (date)
- [ ] Security Reviewer: ___________________________ (date)
- [ ] Compliance Officer: ___________________________ (date)

---

*End of PRD.*
