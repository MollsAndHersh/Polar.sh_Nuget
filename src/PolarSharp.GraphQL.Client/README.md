# PolarSharp.GraphQL.Client

Strawberry Shake codegen helper + bundled schemas for typed .NET GraphQL clients targeting PolarSharp.

## Install

```sh
dotnet add package PolarSharp.GraphQL.Client
```

## Quickstart

```csharp
// 1. Reference this package; it ships introspection JSON + SDL for the Reporting + Catalog schemas.
// 2. Run Strawberry Shake codegen:
//      dotnet graphql generate --schema PolarReporting.graphql --output Generated/Reporting
// 3. Inject the generated client in your host:
services.AddPolarReportingGraphQLClient()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://your-host/graphql/reporting"));
// 4. Use it:
var result = await client.GetCustomerOrders.ExecuteAsync(customerId, ct);
```

## What this package does

Bundles the GraphQL schema definitions (introspection JSON + SDL form) for both PolarSharp GraphQL endpoints — `PolarSharp.Reporting.GraphQL` and `PolarSharp.EcommerceStoreManagement.GraphQL` — so Strawberry Shake's `dotnet graphql generate` command can produce strongly-typed `.NET` client classes against them. Hosts who want a typed GraphQL client for PolarSharp's reporting or catalog APIs reference this package, run codegen, and get typed `IReportingGraphQLClient` / `ICatalogGraphQLClient` interfaces ready to inject.

The bundled schemas track exact PolarSharp GraphQL endpoint versions; the package's `PolarGraphQLClientHelpers.PackageVersion` constant lets host build pipelines verify the schema and the running server agree before codegen runs. Per Case Study 04 "Audience-Scoped Schema Slicing", the schemas shipped here are the *full* surface — runtime audience slicing happens server-side, so generated clients can reference any field even if the caller's audience may not see it at execution time.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 18.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 04: Audience-Scoped Schema Slicing](../../Case%20Studies/04-Audience-Scoped-Schema-Slicing.md)
- `PolarSharp.Reporting.GraphQL` — the reporting endpoint server
- `PolarSharp.EcommerceStoreManagement.GraphQL` — the catalog endpoint server

## License

MIT. (c) Molls and Hersh, LLC. 2026.
