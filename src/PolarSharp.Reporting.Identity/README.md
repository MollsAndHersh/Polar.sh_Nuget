# PolarSharp.Reporting.Identity

Bridges ASP.NET Core Identity sign-in / sign-out events (via `PolarSharp.MultiTenant.Identity`) into the `PolarSharp.Reporting` per-tenant snapshot orchestrator.

When a user signs in to a tenant, the bridge auto-fires `TriggerImmediateAsync(tenantId, "Login")` and `StartPeriodicAsync(tenantId, interval)` on `IReportSnapshotTrigger`. When the user signs out, it calls `StopPeriodicAsync(tenantId)`. A request middleware also calls `Heartbeat(tenantId)` on every authenticated request, so the orchestrator's idle-timeout doesn't kill polling for an active user.

## Install

```sh
dotnet add package PolarSharp.Reporting.Identity
```

Install only when both `PolarSharp.MultiTenant.Identity` AND `PolarSharp.Reporting` (with one of the `.EntityFrameworkCore.*` provider packages) are also in use.

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant().UseSqlServer(connStr)
    .AddPolarReportingSnapshot(builder.Configuration)            // registers IReportSnapshotTrigger
    .AddPolarIdentity(builder.Configuration)                     // registers SignInManager
    .AddPolarReportingIdentityHook();                            // wires the bridge

var app = builder.Build();
app.UseAuthentication();
app.UsePolarReportingIdentityHeartbeat();                        // per-request heartbeat
app.UseAuthorization();
```

That's it — login / logout / heartbeat now drive the snapshot orchestrator automatically.

See the DocFX article at <https://mollsandhersh.github.io/Polar.sh_Nuget/articles/snapshot-identity-hook.html> for the full design rationale.
