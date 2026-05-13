# PolarSharp.Reporting

Per-tenant reporting over Polar.sh data with **JSON-first API**. Pre-built queries for transactions, subscriptions (MRR/ARR/churn), orders (with invoice URLs), customers, customer entitlements, and platform error/audit events. Optional time-series snapshot service mirrors Polar data into local SQL on a configurable schedule.

## Install

```bash
dotnet add package PolarSharp.Reporting.EntityFrameworkCore.SqlServer
# or just PolarSharp.Reporting if you don't need local caching
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant().UseSqlServer(connStr)
    .AddPolarReporting().UseSqlServer(connStr)
        .WithSnapshotSchedule(TimeSpan.FromMinutes(15));
```

## Features

- **Per-tenant scope**: uses the same Finbuckle resolution as webhooks (HTTP context) or background scope hydration (`IHostedService`)
- **JSON-first**: every report has a `GetXxxAsJsonAsync` variant returning pre-serialized JSON for zero-cost forwarding to BI tools
- **Streaming**: `WriteReportAsJsonAsync(Stream output)` for very large reports
- **Snapshot service**: optional `IHostedService` mirrors Polar's `/v1/events/`, `/v1/orders/`, `/v1/subscriptions/`, `/v1/customers/` into local SQL on a schedule
- **AppMasterAdmin platform log**: cross-tenant audit visibility via `IPlatformAuditLog` (gated by `ViewPlatformAuditLog` permission)

See `docs/articles/reporting.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/).

## License

MIT.
