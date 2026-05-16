# PolarSharp.CustomerGraph

Pluggable customer-graph abstraction — typed query API for graph-backed customer relationship analysis.

## Install

```sh
dotnet add package PolarSharp.CustomerGraph
```

## Quickstart

```csharp
// "Find every active customer who has used the same IP as the known-fraud account,
//  ordered by lifetime value, top 100":
var query = new CustomerGraphQuery()
    .WhereSharesIpWith("fraud-2026-04")
    .WhereTaggedAs("active")
    .OrderByLifetimeValue(descending: true)
    .Top(100);
var result = await graphClient.ExecuteAsync(query, ct);
```

Inject `ICustomerGraphQueryClient` from DI after registering a provider (Neo4j, Cosmos Gremlin API, etc.).

## What this package does

Abstracts the customer graph behind a fluent query builder (`CustomerGraphQuery`) + a typed result envelope (`CustomerGraphResult<CustomerNode>`). The builder composes predicates — `WhereCustomerBought`, `WhereInCountry`, `WhereSpentMoreThan`, `WhereSharesIpWith` (fraud-detection), `WhereTaggedAs`, `InLastDays`, etc. — and the provider translates the composed query into the underlying graph database's native language (openCypher for Neo4j, Gremlin for Cosmos's Gremlin API).

Per Case Study 01 "Lift-And-Shift Architecture" the abstraction stays lift-safe: callers code against the typed API, the provider package picks the backend, and migrating from one graph database to another (or moving on/off PolarSharp.* entirely) is a registration-only change. The graph projection (sourced from Polar webhooks + EF SaveChanges interceptors + IP capture) keeps the graph fresh; query latency stays in millisecond territory because graph databases are purpose-built for this access pattern.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 17.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- `PolarSharp.CustomerGraph.Neo4j` — Neo4j provider implementation
- `PolarSharp.CustomerGraph.Projection` — background projection IHostedService
- `PolarSharp.NaturalLanguageQuery.CustomerGraph` — NL -> CustomerGraphQuery generation

## License

MIT. (c) Molls and Hersh, LLC. 2026.
