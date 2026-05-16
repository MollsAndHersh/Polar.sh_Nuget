# PolarSharp.Reporting.GraphQL

Hot Chocolate GraphQL schema for PolarSharp.Reporting — KPIs + hierarchical drilldown via single query.

## Install

```sh
dotnet add package PolarSharp.Reporting.GraphQL
```

## Quickstart

```csharp
builder.Services
    .AddPolarReporting()
    .UsePostgreSqlReporting(connStr);
builder.Services.AddPolarReportingGraphQL();
// ...
app.MapGraphQL("/graphql/reporting");
```

## What this package does

Exposes the reporting drilldown (Customer -> Orders -> Order detail with line items + refunds + benefit grants) plus aggregate KPI queries (Transactions / Subscriptions / Orders / ErrorAudit / Customers / CustomerEntitlements) as a Hot Chocolate 15.x GraphQL schema. Hosts mount the endpoint alongside existing REST routes — typed Strawberry Shake clients consume it from .NET callers; Banana Cake Pop interactive UI handles ad-hoc exploration in Development.

Per Case Study 04 "Audience-Scoped Schema Slicing", the schema is sliced at runtime by the caller's audience tier — SaaSAdmin sees the full schema, Tenant operators see only their tenant's fields, Customer audiences see only their own records. Field-level `[RequirePolarPermission]` integration enforces the slice at the resolver layer; the audience-scoped slice is exactly the field surface the LLM sees in PolarSharp.NaturalLanguageQuery.HotChocolate, so what the user can ask is structurally identical to what they can read.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 18.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 04: Audience-Scoped Schema Slicing](../../Case%20Studies/04-Audience-Scoped-Schema-Slicing.md)
- `PolarSharp.EcommerceStoreManagement.GraphQL` — companion catalog read-side schema
- `PolarSharp.GraphQL.Client` — Strawberry Shake codegen helper for typed .NET clients
- `PolarSharp.NaturalLanguageQuery.HotChocolate` — NL -> GraphQL generation against the same slice

## License

MIT. (c) Molls and Hersh, LLC. 2026.
