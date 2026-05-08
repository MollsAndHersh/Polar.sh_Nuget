# PolarSharp.MultiTenant

Multi-tenant support for the [PolarSharp](https://www.nuget.org/packages/PolarSharp) .NET SDK. Provides per-tenant `PolarClient` isolation backed by Finbuckle.MultiTenant, with independent connection pools, circuit breakers, and rate limiters per tenant.

## Installation

```bash
dotnet add package PolarSharp
dotnet add package PolarSharp.MultiTenant
```

## Quick Start

```csharp
// Program.cs
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant();

app.UsePolarInfrastructure();   // inserts UseMultiTenancy() at correct pipeline position

// appsettings.json
{
  "PolarSharp": {
    "MultiTenant": {
      "Strategy": "Header",
      "Header": { "Name": "X-Tenant-ID" },
      "Tenants": [
        { "Id": "acme", "Identifier": "acme", "Name": "Acme Corp",
          "PolarAccessToken": "tok_live_acme", "Server": "Production" },
        { "Id": "beta", "Identifier": "beta", "Name": "Beta Inc",
          "PolarAccessToken": "tok_sandbox_beta", "Server": "Sandbox" }
      ]
    }
  }
}
```

```csharp
// Per-request usage
app.MapGet("/orders", async (IMultiTenantPolarClientFactory factory, CancellationToken ct) =>
{
    var polar = factory.GetClientForCurrentTenant();   // tenant resolved from X-Tenant-ID header
    return await polar.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct);
});
```

## Features

- **Per-tenant bulkhead isolation** — one tenant's circuit-breaker state never affects others
- **Race-free client creation** — `LazyConcurrentDictionary` guarantees factory runs once per tenant
- **Finbuckle.MultiTenant strategies** — Header, Route, Hostname, Claim (switchable via config)
- **Per-tenant tokens** — each tenant uses its own Polar access token and server (Production/Sandbox)
- **Graceful shutdown** — all per-tenant clients disposed in parallel on `ApplicationStopping`
- **Config-driven** — tenants defined in `appsettings.json`; no code changes to add a tenant

## Tenant Resolution Strategies

| Strategy | Config key | Default |
|---|---|---|
| Header | `PolarSharp:MultiTenant:Header:Name` | `X-Tenant-ID` |
| Route | `PolarSharp:MultiTenant:Route:Parameter` | `tenantId` |
| Hostname | `PolarSharp:MultiTenant:Hostname:Template` | `__tenant__.*` |
| Claim | `PolarSharp:MultiTenant:Claim:Type` | `tid` |

## Documentation

- [Multi-Tenancy Guide](https://markchipman.github.io/PolarSharp/articles/multi-tenancy.html)
- [API Reference](https://markchipman.github.io/PolarSharp/api/)

## License

MIT
