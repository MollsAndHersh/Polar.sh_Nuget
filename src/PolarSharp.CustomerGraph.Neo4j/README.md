# PolarSharp.CustomerGraph.Neo4j

Neo4j provider for PolarSharp.CustomerGraph — per-tenant DATABASE on Neo4j Enterprise; label-based isolation on Community.

## Install

```sh
dotnet add package PolarSharp.CustomerGraph.Neo4j
```

## Quickstart

```csharp
builder.Services.AddPolarCustomerGraphNeo4j(
    boltUri: "bolt://neo4j.internal:7687",
    user: builder.Configuration["Polar:Neo4j:User"]!,
    password: builder.Configuration["Polar:Neo4j:Password"]!);
```

## What this package does

Wires `ICustomerGraphQueryClient` + `ICustomerGraphProjector` to Neo4j via the official .NET driver. Two isolation modes are supported, auto-detected at startup:

- **Neo4j Enterprise (multi-database)**: per-tenant Neo4j DATABASE — the strongest isolation posture, matching PolarSharp's defense-in-depth approach. Tenant data lives in physically separate Neo4j databases; cross-tenant queries are STRUCTURALLY IMPOSSIBLE because the databases do not share an address space.
- **Neo4j Community (single-database)**: label-based isolation via `:Tenant_<id>` labels on every node. The projection writes the tenant label; the query builder enforces a `MATCH` clause on the label for every query. Weaker isolation (relies on query-building discipline) but works on the free tier.

On startup the provider connects, queries the server for multi-database support, and auto-selects. When falling back to label-based isolation, a Warning log explicitly notes the security-posture downgrade. The provider implements the `CustomerGraphQuery` -> openCypher translator. See Case Study 01 "Lift-And-Shift Architecture" for the swap-the-provider posture.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 17.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- `PolarSharp.CustomerGraph` — the abstraction this implements
- `PolarSharp.CustomerGraph.Projection` — feeds Polar webhooks + EF events into the graph
- `PolarSharp.NaturalLanguageQuery.CustomerGraph` — NL -> CustomerGraphQuery generation

## License

MIT. (c) Molls and Hersh, LLC. 2026.
