# Multi-Tenancy

`PolarSharp.MultiTenant` wraps [Finbuckle.MultiTenant](https://www.finbuckle.com/MultiTenant) to give each tenant their own isolated `PolarClient` with its own access token, circuit breaker, connection pool, and rate limiter.

## Installation

```bash
dotnet add package PolarSharp.MultiTenant
```

## Configuration

```json
{
  "PolarSharp": {
    "MultiTenant": {
      "Strategy": "Header",
      "Header": { "Name": "X-Tenant-ID" },
      "Tenants": [
        {
          "Id": "acme",
          "Identifier": "acme",
          "Name": "Acme Corp",
          "PolarAccessToken": "tok_live_acme",
          "Server": "Production"
        },
        {
          "Id": "beta",
          "Identifier": "beta",
          "Name": "Beta Inc",
          "PolarAccessToken": "tok_sandbox_beta",
          "Server": "Sandbox"
        }
      ]
    }
  }
}
```

## Registration

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant();

// ...

app.UsePolarInfrastructure();   // calls app.UseMultiTenant() automatically
```

## Tenant resolution strategies

| Strategy | How tenant is identified | Config key |
|---|---|---|
| `Header` (default) | HTTP request header | `PolarSharp:MultiTenant:Header:Name` (default: `X-Tenant-ID`) |
| `Route` | Route parameter | `PolarSharp:MultiTenant:Route:Parameter` (default: `tenantId`) |
| `Hostname` | Subdomain / hostname | `PolarSharp:MultiTenant:Hostname:Template` (default: `__tenant__.*`) |
| `Claim` | JWT claim value | `PolarSharp:MultiTenant:Claim:Type` (default: `tid`) |

## Using the per-tenant client

Inject `IMultiTenantPolarClientFactory` — it resolves the correct `PolarClient` for the current request's tenant automatically:

```csharp
app.MapGet("/orders", async (IMultiTenantPolarClientFactory factory, CancellationToken ct) =>
{
    var polar = factory.GetClientForCurrentTenant();
    var result = await polar.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct);
    return Results.Ok(result);
});
```

## Bulkhead isolation

Each tenant gets **independent** infrastructure:

- **Connection pool** — one `SocketsHttpHandler` per tenant; one tenant's high throughput doesn't starve others
- **Circuit breaker** — Tenant A's repeated failures open Tenant A's breaker only; Tenant B is unaffected
- **Rate limiter** — per-tenant concurrency cap

This means a single misbehaving tenant cannot cascade failures to other tenants.

## Programmatic tenant registration

For tenants that aren't known at startup:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant(opts =>
    {
        opts.Strategy = TenantStrategy.Claim;
        opts.Claim.Type = "polar_tenant_id";
        opts.Tenants.Add(new PolarTenantInfo
        {
            Id = "enterprise-a",
            Identifier = "enterprise-a",
            Name = "Enterprise A",
            PolarAccessToken = "tok_live_ea",
        });
    });
```
