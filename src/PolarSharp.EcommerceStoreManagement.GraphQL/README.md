# PolarSharp.EcommerceStoreManagement.GraphQL

Hot Chocolate GraphQL schema for the PolarSharp catalog read-side (localized fields, no mutations).

## Install

```sh
dotnet add package PolarSharp.EcommerceStoreManagement.GraphQL
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServer(connStr);
builder.Services.AddPolarCatalogGraphQL();
// ...
app.MapGraphQL("/graphql/catalog");
```

## What this package does

Exposes the local catalog (products, variants, categories, departments, discounts, checkout links, tier groups, business profile) as a Hot Chocolate 15.x GraphQL schema with localized field resolution. The schema is **READ-ONLY by design** — catalog mutations stay on REST endpoints + the publisher orchestrator, so the GraphQL surface doesn't need to mirror the publish workflow's complex idempotency semantics.

Localized field resolution: `product(id, language) { name description }` routes through `IPolarCatalogReader`, which integrates with the v1.2 translation cache — warm-on-read pre-warm preserves cache-hot reads. Tenants can request products in any language they've configured translations for. Per Case Study 04 "Audience-Scoped Schema Slicing", the catalog schema is sliced by audience tier and enforced with field-level `[RequirePolarPermission]` integration so the LLM-driven NL -> GraphQL pipeline (PolarSharp.NaturalLanguageQuery.HotChocolate) cannot reference fields outside the caller's slice.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 18.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 04: Audience-Scoped Schema Slicing](../../Case%20Studies/04-Audience-Scoped-Schema-Slicing.md)
- `PolarSharp.Reporting.GraphQL` — companion reporting read-side schema
- `PolarSharp.GraphQL.Client` — Strawberry Shake codegen helper for typed .NET clients
- `PolarSharp.NaturalLanguageQuery.HotChocolate` — NL -> GraphQL generation against the same slice

## License

MIT. (c) Molls and Hersh, LLC. 2026.
