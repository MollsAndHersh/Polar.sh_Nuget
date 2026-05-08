---
_layout: landing
---

# PolarSharp

A .NET 10 Native AOT-compatible SDK for [Polar.sh](https://polar.sh) — the open-source Merchant of Record payment and monetization platform.

## Installation

```bash
dotnet add package PolarSharp
```

Optional add-ons:

```bash
dotnet add package PolarSharp.Webhooks      # webhook handling + toast notifications
dotnet add package PolarSharp.MultiTenant   # per-tenant client isolation
```

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

## Documentation

- [Getting Started](articles/getting-started.md)
- [Configuration](articles/configuration.md)
- [Webhooks](articles/webhooks.md)
- [Multi-Tenancy](articles/multi-tenancy.md)
- [NuGet Deployment](articles/nuget-deployment.md)
- [Security](articles/security.md)
- [API Reference](api/index.md)

## Features

| Feature | Description |
|---|---|
| Full API surface | 25+ resource areas generated from the OpenAPI spec via Kiota |
| Native AOT | Zero reflection; publishes with `dotnet publish -p:PublishAot=true` |
| Resilience | Retry, circuit breaker, timeout, hedging |
| Idempotency | Automatic `X-Idempotency-Key` on mutating requests |
| API versioning | `Polar-Version` date-pinned header; mismatch detection at startup |
| Test/Live mode | Startup banner + token-prefix sanity check |
| Result monad | `Result<TValue, PolarError>` — no exceptions on 4xx |
| Health checks | `IHealthCheck` integration; tag `"polar"` |
| Observability | `ActivitySource` + `IMeterFactory` metrics |
| Localization | Built-in `en-US` and `es-MX` |
| Strong-named | Assemblies signed; NuGet package signing in CI |

## License

MIT — [View on GitHub](https://github.com/mollsandhersh/Polar.sh_Nuget)
