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

## Tenant lifecycle (v1.2.x addition)

`PolarTenantInfo` carries lifecycle state and site-manager contact information so that suspension, deactivation, soft-deletion, and reactivation can drive consistent downstream effects (notifications, Litestream replication adjustments, audit logging).

New properties on `PolarTenantInfo`:

| Property | Type | Notes |
|---|---|---|
| `Status` | `TenantStatus` | `Active` (default), `Suspended`, `Inactive`, `Deleted` |
| `IsActive` | `bool` (computed) | Shortcut for `Status == TenantStatus.Active` |
| `SiteManagerEmail` | `string` | Required; recipient of lifecycle notifications |
| `SiteManagerEmailVerified` | `bool` | Defaults `false`; gates suspension by default |
| `SiteManagerPhone` | `string?` | Optional E.164 (e.g., `+15555551234`) for SMS |

### Changing status — `ITenantStatusService`

All lifecycle transitions go through `ITenantStatusService`. Mutating `Status` directly persists the change but skips the notification pipeline, so subscribers will not react. Prefer the service:

```csharp
builder.Services
    .AddPolarMultiTenant(...)
    .AddPolarTenantLifecycle(builder.Configuration);

// In a SaaS admin endpoint:
public sealed class SuspendTenantHandler(ITenantStatusService status)
{
    public async Task<IResult> Handle(Guid tenantId, string reason, CancellationToken ct)
    {
        var result = await status.SuspendAsync(tenantId, reason, ct: ct);
        return result.Success
            ? Results.Ok(new { result.PreviousStatus, result.NewStatus, result.WasIdempotentNoOp })
            : Results.BadRequest(new { result.FailureReason });
    }
}
```

Each call returns `TenantStatusChangeResult`. Idempotent no-ops (e.g., suspending an already-suspended tenant) return `Success = true` with `WasIdempotentNoOp = true` and do NOT fire the notification.

### Reacting to status changes — `TenantStatusChangedNotification`

Status transitions publish a MediatR `TenantStatusChangedNotification`. Implement `INotificationHandler<TenantStatusChangedNotification>` to react:

```csharp
public sealed class LogStatusChangeHandler(ILogger<LogStatusChangeHandler> log)
    : INotificationHandler<TenantStatusChangedNotification>
{
    public Task Handle(TenantStatusChangedNotification n, CancellationToken ct)
    {
        log.LogInformation(
            "Tenant {TenantId} ({Identifier}) {Previous} -> {New}: {Reason}",
            n.TenantId, n.TenantIdentifier, n.PreviousStatus, n.NewStatus, n.Reason);
        return Task.CompletedTask;
    }
}
```

`AddPolarTenantLifecycle` scans the `PolarSharp.MultiTenant` assembly for handlers; host applications register their own handlers from their own assemblies via the standard `AddMediatR(...)` call. MediatR de-duplicates assembly scans, so the two registrations coexist.

### `RequireVerifiedEmailForSuspension`

By default, the service refuses to suspend a tenant whose `SiteManagerEmailVerified` is `false` — the suspension notification would be unverifiable. Override either policy:

```json
{
  "PolarSharp": {
    "MultiTenant": {
      "TenantStatus": {
        "RequireVerifiedEmailForSuspension": false,
        "SuspendUnverifiedTenantsAnyway": false,
        "DeletedTenantRetentionDays": 90
      }
    }
  }
}
```

Two flags are intentionally separate so an audit trail captures whether you globally relaxed the policy (`RequireVerifiedEmailForSuspension: false`) or made a deliberate per-deployment exception (`SuspendUnverifiedTenantsAnyway: true`).

### Email verification flow

The verification flow itself — generating a one-time link, delivering it, processing the click — is deferred to a future v1.2.x release. In the interim, hosts can manually set `SiteManagerEmailVerified = true` for tenants they trust (e.g., onboarded via a back-office process).

## DI wiring (no manual MediatR registration needed)

`AddPolarMultiTenant(...)` and `AddPolarTenantLifecycle(...)` register MediatR internally for this package's own assembly. Host code does **not** need to call `services.AddMediatR(...)` directly — the lifecycle notification pipeline (`TenantStatusChangedNotification` and its handlers) is wired up by the package itself. The correct DI wiring from a host application is just:

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant()                                     // registers MT services + MediatR (this package's assembly)
    .AddPolarTenantLifecycle(builder.Configuration);           // adds ITenantStatusService + lifecycle handler discovery
```

If the host application **also** uses MediatR for its own handlers, calling `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MyHostAssemblyMarker).Assembly))` from host code composes cleanly with the PolarSharp registration. MediatR's `AddMediatR` is idempotent across multiple calls — handlers from every registered assembly are discoverable, so multiple PolarSharp packages and the host can each register their own assemblies without conflict.

For the full decision tree on which `AddPolarXxx(...)` extensions to call in which scenario, see the [`Choosing your PolarSharp DI wiring`](../../../docs/articles/narratives/choosing-your-polarsharp-di-wiring.md) Implementation Narrative.

## Documentation

- [Multi-Tenancy Guide](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/multi-tenancy.html)
- [API Reference](https://mollsandhersh.github.io/Polar.sh_Nuget/api/)

## License

MIT
