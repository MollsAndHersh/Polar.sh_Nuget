# PolarSharp.NaturalLanguageQuery.CustomerGraph

CustomerGraphQuery target for natural-language to typed graph query generation.

## Install

```sh
dotnet add package PolarSharp.NaturalLanguageQuery.CustomerGraph
```

## Quickstart

```csharp
// Inject ICustomerGraphNaturalLanguageGenerator after registering the router + this target:
var request = new NaturalLanguageQueryRequest(
    NaturalLanguageInput: "customers in Germany who bought product P-101 and spent over EUR 500",
    AudienceScope: AudienceScope.Tenant,
    TenantId: currentTenant.Id,
    UserId: currentUser.Id);
var response = await generator.GenerateAsync(request, ct);
// response.GeneratedQueryDocument is JSON describing the typed CustomerGraphQuery.
```

## What this package does

Generates typed `CustomerGraphQuery` objects from natural-language input. The LLM emits a JSON tree describing the desired query in terms of the fluent builder DSL, which gets deserialized into a `CustomerGraphQuery` instance via a sealed-record schema — **the LLM cannot inject raw openCypher / Gremlin / SQL into the query path**, only structured predicates the builder already knows how to express.

Per Case Study 04 "Audience-Scoped Schema Slicing", audience-scoped builder filters apply: the Customer audience cannot use `WhereSharesIpWith` or any IP-edge predicates; the Tenant audience cannot reference cross-tenant fraud predicates; SaaSAdmin sees the full builder surface. The LLM only sees the predicates it's allowed to compose — **the audience slice IS the LLM's allowed-builder-surface schema**, structurally enforced rather than instruction-enforced. Same `IAiCompletionClient` abstraction as the catalog translation pipeline so hosts re-use whichever LLM provider they already chose.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 19.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 04: Audience-Scoped Schema Slicing](../../Case%20Studies/04-Audience-Scoped-Schema-Slicing.md)
- `PolarSharp.NaturalLanguageQuery` — the router + request/response types
- `PolarSharp.NaturalLanguageQuery.HotChocolate` — GraphQL alternative target
- `PolarSharp.CustomerGraph` — the typed query DSL the LLM emits

## License

MIT. (c) Molls and Hersh, LLC. 2026.
