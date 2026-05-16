# PolarSharp.Reporting.Marten

Marten-backed event-sourced reporting events (Postgres-only).

## Install

```sh
dotnet add package PolarSharp.Reporting.Marten
```

## Quickstart

```csharp
builder.Services
    .AddPolarReporting()
    .UsePostgreSqlReporting(connStr);
builder.Services.UseMartenReporting(connStr);
// Optional second arg: schemaName (defaults to "polar_marten_reporting")
```

## What this package does

Replaces the EF Core-backed reporting backend with Marten's event-sourcing primitives (append-only streams, projection daemon, snapshots). Polar's `/v1/events/` stream is itself event-sourced — Polar emits an immutable log of every business event (`order.created`, `subscription.canceled`, `refund.completed`, etc.) — so mirroring that into Marten is a natural fit: native event-streaming semantics align with the source data's append-only shape.

Hosts who choose this provider get: zero impedance mismatch with Polar's data model, online projection rebuilds (no maintenance window), and snapshot support for fast point-in-time reads. Marten + PolarSharp's RLS-enabled Postgres providers can coexist on the same database instance — Marten's session sets `app.current_tenant_id` so the v1.3 RLS policies enforce tenant isolation on Marten-driven queries as well.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 15.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md) — same event-sourcing primitives
- `PolarSharp.AuditLog.Marten` — Marten alternative for audit log storage
- `PolarSharp.Onboarding.Wizard.Marten` — Marten alternative for onboarding session storage
- `PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL` — the EF Core path this replaces

## License

MIT. (c) Molls and Hersh, LLC. 2026.
