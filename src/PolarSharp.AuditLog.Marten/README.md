# PolarSharp.AuditLog.Marten

Marten-backed event-sourced AdminAuditLogEntry storage (Postgres-only; replaces the EF Core impl when host opts in).

## Install

```sh
dotnet add package PolarSharp.AuditLog.Marten
```

## Quickstart

```csharp
builder.Services
    .AddPolarIdentity(builder.Configuration)
    .UsePostgreSql(connStr);
builder.Services.UseMartenAuditLog(connStr);
// Optional second arg: schemaName (defaults to "polar_marten_auditlog")
```

## What this package does

Replaces the EF Core-backed audit log (`PolarSharp.MultiTenant.Identity`'s `polar_admin_audit_log` table) with a Marten event-sourced implementation. Hosts opt into this provider when they:

- Already run on PostgreSQL (Marten is Postgres-only).
- Want native event-streaming semantics — append-only events, projections, snapshots — rather than EF Core's mutable-row approach.
- Are willing to operate Marten's projection daemon alongside their normal database lifecycle.

Marten coexists with the v1.3 PolarSharp Postgres EF Core providers — both can run against the same Postgres instance in different schemas. The Marten document store session sets the same `app.current_tenant_id` session variable that `PostgreSqlTenantSessionInterceptor` sets for the EF Core path, so the RLS policies provisioned by the v1.3 `EnableRowLevelSecurity` migrations also enforce isolation on Marten-driven queries — defense-in-depth carries over to the event-sourced layer.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 15.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md) — same event-sourcing primitives
- `PolarSharp.MultiTenant.Identity.PostgreSQL` — the EF Core audit-log path Marten replaces
- `PolarSharp.Reporting.Marten` — Marten alternative for reporting snapshots
- `PolarSharp.Onboarding.Wizard.Marten` — Marten alternative for onboarding session storage

## License

MIT. (c) Molls and Hersh, LLC. 2026.
