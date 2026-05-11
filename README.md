# PolarSharp

> A .NET 10 Native AOT-compatible SDK for [Polar.sh](https://polar.sh) — the open-source Merchant of Record payment and monetization platform.

[![PolarSharp](https://img.shields.io/github/v/release/mollsandhersh/Polar.sh_Nuget?label=PolarSharp&color=blue)](https://github.com/mollsandhersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp)
[![PolarSharp.Webhooks](https://img.shields.io/github/v/release/mollsandhersh/Polar.sh_Nuget?label=PolarSharp.Webhooks&color=blue)](https://github.com/mollsandhersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Webhooks)
[![PolarSharp.MultiTenant](https://img.shields.io/github/v/release/mollsandhersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant&color=blue)](https://github.com/mollsandhersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant)
[![PolarSharp.Templates](https://img.shields.io/github/v/release/mollsandhersh/Polar.sh_Nuget?label=PolarSharp.Templates&color=blue)](https://github.com/mollsandhersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Templates)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-github.io-informational)](https://mollsandhersh.github.io/Polar.sh_Nuget/)

> **Distributed via [GitHub Packages](https://github.com/mollsandhersh/Polar.sh_Nuget/packages).** See [Installing from GitHub Packages](#installing-from-github-packages) below.

---

## The Problem Nobody Talks About

You've chosen [Polar.sh](https://polar.sh) as your Merchant of Record. Smart move — they handle VAT, tax, compliance, and all the stuff that keeps accountants employed. You fire up your .NET project, head to the docs, and discover the official SDKs. Python? ✅ JavaScript? ✅ .NET? ...🦗

So you do what .NET developers do: you write it yourself. An `HttpClient`. Some DTOs. A rough stab at HMAC webhook verification. Retry logic that kind of works. A multi-tenant setup you're not entirely confident in. Six weeks later you have something that mostly works, except for that one edge case where a webhook gets processed twice and charges a customer twice. Whoops.

**PolarSharp exists so you never have to write any of that.**

---

## What You Get

Three focused NuGet packages that cover every operational concern a serious .NET application needs when integrating with Polar:

| Package | What it does |
|---|---|
| `PolarSharp` | Full Polar.sh API client — 25+ resource areas, enterprise resilience, observability, AOT |
| `PolarSharp.Webhooks` | HMAC-verified event handling, toast notifications, background queues, reconciliation |
| `PolarSharp.MultiTenant` | Per-tenant client isolation with independent circuit breakers and connection pools |
| `PolarSharp.Templates` | `dotnet new` template pack — scaffold any webhook handler in one command |

---

## Features That Actually Matter

### It handles the boring stuff automatically

- **Idempotency keys** on every mutating request, stable across retries — no accidental double-charges when your circuit breaker kicks in
- **Retry + circuit breaker + timeout** via `Microsoft.Extensions.Http.Resilience` — Polar having a bad moment won't take your whole app down with it
- **`Polar-Version` header pinning** — date-lock your API contract so Polar's schema evolution doesn't silently break your production app
- **Token hot-reload** via `IOptionsMonitor` — rotate your access token by updating config; zero app restart required
- **Test/Live mode banner** at startup with a token-prefix sanity check, because accidentally charging real customers with a sandbox token (or vice versa) is a very bad day

### The error handling is actually ergonomic

No exceptions for recoverable HTTP errors. Every resource method returns `Result<TValue, PolarError>`:

```csharp
app.MapGet("/orders/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
    (await polar.Orders.EmptyPathSegment[id].GetAsync(cancellationToken: ct))
        .Match(
            onSuccess: order => Results.Ok(order),
            onFailure: error  => error.ToHttpResult()));  // maps 401/403/404/422/429 correctly
```

No try-catch. No `if (response.IsSuccessStatusCode)`. Just a clean result you pattern-match against.

### Webhooks that don't keep you up at night

```csharp
// Register
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent, OrderCreatedHandler>();

// Implement
public sealed class OrderCreatedHandler : PolarWebhookHandlerBase<OrderCreatedEvent>
{
    protected override Task HandleCoreAsync(OrderCreatedEvent @event, CancellationToken ct)
        => _orders.FulfillAsync(OrderId.From(@event.Data.Id), ct);
}
```

Polar delivers webhooks **at-least-once**. PolarSharp verifies every HMAC signature (timing-uniform — no oracle attacks), validates timestamps against replay attacks, exposes the `WebhookId` for your idempotency check, and lets you swap in a background queue (`enqueue: true`) for handlers that need more than 30 seconds to complete. It also warns you at startup if you forgot to register a handler for a known event type, because finding out at 2am that subscription cancellations have been silently discarded for three weeks is not fun.

### Multi-tenancy that actually isolates tenants

One misbehaving tenant — hammering Polar with bad requests, tripping a circuit breaker — must not freeze out all your other tenants. Each tenant gets its own `HttpClient`, its own connection pool, its own circuit breaker state, and its own rate limiter budget. When Tenant A's breaker opens, Tenant B continues serving requests without even noticing.

### Native AOT — for real

Zero reflection in hot paths. Source-generated JSON. Static event type lists (no `Assembly.GetTypes()`). Explicit `IValidateOptions<T>` instead of `ValidateDataAnnotations()`. CI gates `dotnet publish -p:PublishAot=true` with zero warnings on every PR. If you're deploying to Azure Container Apps, AWS Lambda, or anything else where cold-start time and binary size matter, PolarSharp won't be the thing that breaks your AOT build.

### Observability without a config tax

```csharp
// That's it. This is the entire observability setup.
builder.Services.AddPolarInfrastructure(builder.Configuration);
```

Every API call emits an `ActivitySource("PolarSharp")` span, compatible with any OpenTelemetry backend. Every operation increments `Meter("PolarSharp")` counters and histograms. An `IHealthCheck` (tag: `"polar"`) shows up in your `/health` endpoint. Structured `ILogger.BeginScope` scopes attach `polar.request_id`, `polar.tenant_id`, `polar.resource`, and `polar.operation` to every log entry — with automatic PII redaction for customer emails and names.

---

## Installing from GitHub Packages

PolarSharp is distributed via [GitHub Packages](https://github.com/mollsandhersh/Polar.sh_Nuget/packages), not NuGet.org. Add the feed to your project's `NuGet.config` first (create this file at your solution root if it doesn't exist):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="PolarSharp" value="https://nuget.pkg.github.com/mollsandhersh/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <PolarSharp>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </PolarSharp>
  </packageSourceCredentials>
</configuration>
```

The `YOUR_GITHUB_PAT` is a [GitHub Personal Access Token](https://github.com/settings/tokens) with the `read:packages` scope — a read-only token is sufficient and safe to commit to CI secrets. Then install the packages:

```bash
# Core SDK — required
dotnet add package PolarSharp

# Webhook handling, toast notifications, background queues, reconciliation — optional
dotnet add package PolarSharp.Webhooks

# Per-tenant client isolation with Finbuckle.MultiTenant — optional
dotnet add package PolarSharp.MultiTenant

# dotnet new templates for scaffolding webhook handlers — optional
dotnet new install PolarSharp.Templates
```

### Minimum configuration

```json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx"
  }
}
```

```csharp
// Program.cs — single call wires everything
builder.Services.AddPolarInfrastructure(builder.Configuration);

app.UsePolarInfrastructure();
```

### Full stack with webhooks and multi-tenancy

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent,       OrderCreatedHandler>()
    .AddWebhookHandler<SubscriptionActiveEvent, SubscriptionHandler>()
    .AddPolarToastNotifications()
    .AddPolarMultiTenant();

app.UsePolarInfrastructure();
```

### Full configuration reference

```json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx",
    "ApiVersion": "2025-01-15",
    "ApiVersionStrictness": "Warn",
    "TimeoutMs": 30000,
    "MaxRetries": 3,
    "Resilience": {
      "CircuitBreakerFailureThreshold": 5,
      "CircuitBreakerSamplingSeconds": 30,
      "CircuitBreakerBreakSeconds": 15,
      "HedgeAfterMs": null
    },
    "Connection": {
      "MaxConnectionsPerServer": 100,
      "PooledConnectionLifetimeMinutes": 15,
      "EnableHttp2": true,
      "EnableHttp3": false
    },
    "Webhooks": {
      "Secrets": ["whsec_xxx"],
      "Path": "/hooks/polar",
      "RequireHttps": true,
      "ToleranceSeconds": 300,
      "ToastNotifications": {
        "Enabled": true,
        "Events": [
          {
            "EventType": "order.created",
            "Title": "New Order",
            "MessageTemplate": "Order #{OrderNumber} from {CustomerEmail}",
            "Severity": "Success",
            "DurationSeconds": 5
          }
        ]
      }
    },
    "MultiTenant": {
      "Strategy": "Header",
      "Header": { "Name": "X-Tenant-ID" },
      "Tenants": [
        {
          "Id": "acme",
          "Identifier": "acme",
          "PolarAccessToken": "tok_live_acme",
          "Server": "Production"
        }
      ]
    }
  }
}
```

---

## Scaffold a Webhook Handler in One Command

```bash
dotnet new install PolarSharp.Templates

dotnet new polar-handler --event OrderCreatedEvent --name OrderCreatedHandler
```

Generates a complete, XML-documented, compilable handler class with all available event data properties listed in the doc comments. Covers all 28 Polar event types.

---

## Local Development Setup

Before running PolarTestApp you need three things: a Polar sandbox token, a webhook secret, and a tunnel so Polar can reach your localhost.

### 1 — Get a Polar sandbox access token

Sign in at [polar.sh](https://polar.sh) → **Settings → Developers → Access Tokens** → create a token. Sandbox tokens start with `polar_oat_`.

### 2 — Store credentials via user-secrets

Run these from inside `testapp/PolarTestApp/`:

```bash
dotnet user-secrets set "PolarSharp:AccessToken" "polar_oat_***"
dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_placeholder"
```

Secrets are stored in `~/.microsoft/usersecrets/` and are never committed to the repo.

### 3 — Install and configure ngrok

Polar's servers cannot reach `localhost` directly — you need a public tunnel.

```bash
brew install ngrok/ngrok/ngrok
```

ngrok requires a free account. Sign up at [dashboard.ngrok.com/signup](https://dashboard.ngrok.com/signup), then configure your authtoken (one-time setup):

```bash
ngrok config add-authtoken YOUR_AUTHTOKEN_HERE
```

Start the tunnel (keep this terminal open):

```bash
ngrok http 5115
```

ngrok prints a public URL like `https://a1b2c3.ngrok-free.app`. This URL changes each time you restart ngrok on the free tier.

### 4 — Register the webhook endpoint in Polar

In the Polar dashboard go to **Settings → Webhooks → Add endpoint**. Set the URL to:

```
https://YOUR-NGROK-URL.ngrok-free.app/hooks/polar
```

Polar generates a `whsec_` secret — copy it and update your user-secret:

```bash
dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_***"
```

Then restart the app. See the full guide — [Local Development Setup](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/local-development.html) — for a complete step-by-step walkthrough including how to send test events.

---

## Documentation

Full documentation — conceptual articles, configuration reference, and complete API reference — is published at:

**[https://mollsandhersh.github.io/Polar.sh_Nuget/](https://mollsandhersh.github.io/Polar.sh_Nuget/)**

The site covers:

| Article | What it answers |
|---|---|
| [Getting Started](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/getting-started.html) | Full annotated `Program.cs` and first API call |
| [Local Development Setup](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/local-development.html) | Sandbox token, user-secrets, ngrok tunnel, webhook testing |
| [Configuration](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/configuration.html) | Every `appsettings.json` field with valid values and defaults |
| [Webhooks](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/webhooks.html) | HMAC verification, event types, handler registration |
| [Webhook Handlers](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/webhook-handlers.html) | `PolarWebhookHandlerBase<T>`, background queues, idempotency |
| [Multi-Tenancy](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/multi-tenancy.html) | Finbuckle strategies, per-tenant bulkhead isolation |
| [Toast Notifications](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/toast-notifications.html) | `IPolarToastChannel`, Blazor/SignalR/SSE integration, lazy localization |
| [API Versioning](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/api-versioning.html) | Date-pinned headers, mismatch detection, strictness modes |
| [Security](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/security.html) | Webhook hardening checklist, IP allowlist, token rotation, anomaly detection |
| [Performance Tuning](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/performance-tuning.html) | Connection pool sizing, HTTP/2, hedging, benchmarks |
| [Middleware Pipeline Ordering](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/pipeline-ordering.html) | Exactly where `UsePolarInfrastructure()` goes and why |
| [Test vs Live Mode](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/test-vs-live-mode.html) | Mode banner, token-prefix checks, switching safely |
| [NuGet Deployment](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/nuget-deployment.html) | Publishing to NuGet.org, GitHub Packages, release tagging |
| [Localization](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/localization.html) | `IPolarLocalizer`, built-in `en-US`/`es-MX`, adding new languages |
| [API Reference](https://mollsandhersh.github.io/Polar.sh_Nuget/api/) | Full XML-documented API reference for all three packages |

---

## Packages at a Glance

### `PolarSharp` — Core SDK

The foundation. Everything else builds on top of this.

- **25+ resource areas** — Orders, Subscriptions, Customers, Products, Checkouts, Benefits, Refunds, Discounts, Meters, License Keys, Customer Sessions, Customer Portal, and more — generated from Polar's OpenAPI spec via Kiota
- **`Result<TValue, PolarError>`** returns on every method — typed errors, no exception-for-control-flow
- **`Option<T>`** for nullable fields — explicit, no surprise `NullReferenceException`
- **Automatic retry, circuit breaker, timeout, hedging** (GET/HEAD) via `Microsoft.Extensions.Http.Resilience`
- **Auto idempotency keys** stable across all retry attempts
- **`Polar-Version` date-pinned header** — lock to a known API schema at startup
- **HTTP/2 with connection pooling** and DNS rotation out of the box
- **OpenTelemetry** spans + metrics + health check included
- **PII redaction** in structured logs — customer emails/names never land in your log aggregator
- **Localized error messages** — built-in `en-US` and `es-MX`, extensible via `IPolarLocalizer`
- **Native AOT** — zero ILC warnings, `IsAotCompatible=true`, `IsTrimmable=true`

### `PolarSharp.Webhooks` — Webhook Integration

- **HMAC-SHA256 signature verification** per [Standard Webhooks](https://www.standardwebhooks.com/) spec
- **Multi-secret rotation** — old and new secrets active simultaneously during zero-downtime rotation
- **28 strongly-typed event records** — `OrderCreatedEvent`, `SubscriptionActiveEvent`, `RefundCreatedEvent`, etc.
- **Startup completeness check** — warns (or fails) at launch if known event types have no handler
- **Background queue adapter** (`enqueue: true`) — returns 200 immediately, processes off the request path
- **Webhook reconciliation** — periodic replay of missed events via `polar.Events.ListAsync` for recovery from outages
- **Real-time toast notifications** via `IPolarToastChannel` — feed any Blazor, SignalR, or SSE UI
- **Security hardening** — payload size cap (1 MB), rate limiting, IP allowlist, content-type enforcement, timing-uniform error responses, anomaly detection metrics
- **In-memory idempotency dedup** — optional safety net for at-least-once delivery

### `PolarSharp.MultiTenant` — Multi-Tenant Isolation

- **Per-tenant `PolarClient` instances** — each with its own token, server, and connection pool
- **Per-tenant circuit breakers and rate limiters** via `ResiliencePipelineRegistry<string>` — tenant A's failures are completely invisible to tenant B
- **Race-free initialization** via `LazyConcurrentDictionary` — factory runs exactly once per tenant under any concurrency
- **Finbuckle.MultiTenant integration** — Header, Route, Hostname, or Claim resolution; configured entirely from `appsettings.json`
- **Graceful shutdown** — all per-tenant clients disposed in parallel on `ApplicationStopping`

---

## Compatibility

| Target | Supported |
|---|---|
| .NET 10 | ✅ |
| Native AOT | ✅ |
| ASP.NET Core Minimal API | ✅ |
| ASP.NET Core MVC | ✅ |
| Blazor Server | ✅ |
| Azure Functions (isolated worker) | ✅ |
| Worker Service / `IHostedService` | ✅ |

---

## Disclaimer and Legal Notices

### No Affiliation with Polar.sh

PolarSharp is an independent, community-developed open-source library. The author has **no affiliation, partnership, sponsorship, or relationship of any kind** with Polar.sh, Polar Software Inc., or the operators of the polar.sh website. PolarSharp is not endorsed by, certified by, or in any way associated with Polar.sh. All Polar.sh trademarks, service marks, and brand names are the property of their respective owners.

### No Warranties — Use at Your Own Risk

THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, ACCURACY, RELIABILITY, OR NON-INFRINGEMENT. THE AUTHOR MAKES NO REPRESENTATIONS OR WARRANTIES THAT THIS LIBRARY:

- meets any particular technical, regulatory, compliance, or business requirements;
- is free of defects, bugs, or security vulnerabilities, known or unknown;
- will function correctly with any particular version of the Polar.sh API;
- is suitable for use in production, financial, medical, legal, or any other regulated environment; or
- has been independently audited, vetted, reviewed, or tested by any person or organization other than the author.

**USE OF THIS LIBRARY IS ENTIRELY AT YOUR OWN RISK.** You are solely responsible for evaluating the suitability of this software for your use case and for all consequences arising from its use.

### Independent Testing Disclosure

PolarSharp has been designed and tested by its author to the best of their ability. It has **not** been independently audited, penetration-tested, security-reviewed, or certified by any third-party security firm, compliance body, or independent testing organization. No claim of security certification or compliance certification (PCI DSS, SOC 2, ISO 27001, GDPR, or otherwise) is made for this library.

### Security Features — No Guarantee

PolarSharp was designed with enterprise-class security in mind and incorporates significant defensive measures including, but not limited to: HMAC-SHA256 webhook signature verification, timing-uniform error responses, payload size enforcement, rate limiting, IP allowlisting, TLS 1.2+ enforcement with certificate revocation checking, SSRF mitigation, and anomaly detection metrics. These features are intended to provide meaningful protection against common attack vectors including denial-of-service attacks, replay attacks, and webhook forgery.

**However, no software can guarantee protection against all known or unknown attack vectors.** The threat landscape evolves continuously, and no representation is made that the security controls in this library will be effective against all present or future attack techniques. It is your responsibility to perform your own security assessment, keep dependencies up to date, monitor for vulnerabilities, and apply additional controls appropriate to your environment and risk tolerance.

### Limitation of Liability

IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY CLAIM, DAMAGES, OR OTHER LIABILITY — WHETHER IN AN ACTION OF CONTRACT, TORT, OR OTHERWISE — ARISING FROM, OUT OF, OR IN CONNECTION WITH THIS SOFTWARE OR THE USE OR OTHER DEALINGS IN THIS SOFTWARE, INCLUDING ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, CONSEQUENTIAL, OR PUNITIVE DAMAGES, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGES. This includes, without limitation, any financial losses, fraudulent transactions, data breaches, regulatory penalties, or business interruptions arising from the use or inability to use this software.

### Third-Party Services

This library communicates with the Polar.sh API. Your use of Polar.sh is subject to Polar.sh's own terms of service, privacy policy, and acceptable use policy, which are independent of and unrelated to this library and its author.

---

## License

MIT — see [LICENSE](LICENSE).

---

*Built with Kiota, Microsoft.Extensions.Http.Resilience, and Finbuckle.MultiTenant.*
*Fully generated API reference and conceptual docs at [mollsandhersh.github.io/Polar.sh_Nuget](https://mollsandhersh.github.io/Polar.sh_Nuget/).*
