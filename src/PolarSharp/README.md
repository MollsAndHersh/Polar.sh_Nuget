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
    .AddPolarInfrastructure(builder.Configuration);   // reads PolarSharp section

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

## Features

- **Full Polar.sh API surface** — 25+ resource areas, generated from the OpenAPI spec via Kiota
- **Native AOT** — zero reflection; publishes with `dotnet publish -p:PublishAot=true` with zero warnings
- **Resilience** — retry, circuit breaker, timeout, hedging via `Microsoft.Extensions.Http.Resilience`
- **Idempotency** — automatic `X-Idempotency-Key` header on mutating requests; stable across retries
- **API versioning** — `Polar-Version` date-pinned header; mismatch detection at startup
- **Test/Live mode** — startup banner + token-prefix sanity check guards real transactions
- **Result monad** — `Result<TValue, PolarError>` return type; no exceptions on 4xx
- **Health checks** — `IHealthCheck` integration; tag `"polar"`
- **Observability** — `ActivitySource` + `IMeterFactory` metrics; structured `ILogger.BeginScope` scopes
- **Localization** — `IPolarLocalizer`; built-in `en-US` and `es-MX`
- **Strong-named** — assemblies signed; NuGet package signing in CI

## Configuration

```json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx",
    "TimeoutMs": 30000,
    "MaxRetries": 3,
    "ApiVersion": "2025-01-15",
    "Connection": {
      "MaxConnectionsPerServer": 100,
      "EnableHttp2": true
    }
  }
}
```

See the [full configuration reference](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/configuration.html) for all options.

## Documentation

- [Getting Started](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/getting-started.html)
- [API Reference](https://mollsandhersh.github.io/Polar.sh_Nuget/api/)
- [Webhooks](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/webhooks.html)
- [Multi-Tenancy](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/multi-tenancy.html)
- [Security](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/security.html)
- [NuGet Deployment](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/nuget-deployment.html)

## License

MIT
