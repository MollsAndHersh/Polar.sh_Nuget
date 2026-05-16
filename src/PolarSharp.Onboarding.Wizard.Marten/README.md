# PolarSharp.Onboarding.Wizard.Marten

Marten-backed event-sourced OnboardingSession storage (Postgres-only).

## Install

```sh
dotnet add package PolarSharp.Onboarding.Wizard.Marten
```

## Quickstart

```csharp
builder.Services.UseMartenOnboardingWizard(connStr);
// Optional second arg: schemaName (defaults to "polar_marten_wizard")
```

## What this package does

Replaces the EF Core-backed onboarding-wizard session storage with a Marten event-sourced implementation. An onboarding session is a sequence of step submissions over time — a natural fit for an append-only event stream. Marten's projection support lets the wizard's "current state" view be rebuilt from the event stream at any point, and snapshots make resume-after-days-of-pause fast (no full replay).

Postgres-only — Marten is Postgres-native. Hosts running on other databases stay on the EF Core onboarding session path. Marten coexists with the v1.3 PolarSharp Postgres EF Core providers on the same database instance in a separate schema; the same `app.current_tenant_id` session variable that drives RLS for the EF Core path also flows through Marten's session, so the v1.3 RLS policies enforce tenant isolation on Marten-driven onboarding session queries as well.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 15.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md) — same event-sourcing primitives
- `PolarSharp.Onboarding` — the EF Core path this replaces
- `PolarSharp.AuditLog.Marten` — Marten alternative for audit log storage
- `PolarSharp.Reporting.Marten` — Marten alternative for reporting events

## License

MIT. (c) Molls and Hersh, LLC. 2026.
