# PolarSharp.NaturalLanguageQuery

Abstractions for LLM-driven natural-language to query generation (audience-scoped schema slicing + authorization gate + cost guards).

## Install

```sh
dotnet add package PolarSharp.NaturalLanguageQuery
```

## Quickstart

```csharp
// Inject INaturalLanguageQueryRouter after registering a target backend
// (PolarSharp.NaturalLanguageQuery.HotChocolate and/or .CustomerGraph):
var request = new NaturalLanguageQueryRequest(
    NaturalLanguageInput: "top 10 customers by spend last 30 days",
    AudienceScope: AudienceScope.Tenant,
    TenantId: currentTenant.Id,
    UserId: currentUser.Id);
var response = await router.TranslateAndExecuteAsync(request, ct);
// response.GeneratedQueryDocument; response.ResultsJson; response.AuditEntryId
```

## What this package does

Defines the contract for converting plain-English questions into executable queries (GraphQL documents OR typed `CustomerGraphQuery` objects). Per Case Study 04 "Audience-Scoped Schema Slicing", the LLM is constrained by the **schema slice** — the subset of the full schema visible to the caller's audience tier. The LLM literally cannot reference fields outside the slice because they are not in the prompt the LLM sees.

The router orchestrates the full pipeline: intent classifier picks the target (GraphQL vs CustomerGraphQuery), schema slice computed from `AudienceScope` + permissions, structured-output enforcement constrains LLM output, generated query dry-run validated for authorization, cost / complexity gate (max depth, max field count, RU/row estimate, timeout), optional execution, audit-log entry with the full breakdown. Three audience tiers — SaaSAdmin, Tenant, Customer — each get a different slice; the response is identical in shape across tiers so callers can switch audience without rewriting their UI.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 19.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 04: Audience-Scoped Schema Slicing](../../Case%20Studies/04-Audience-Scoped-Schema-Slicing.md)
- `PolarSharp.NaturalLanguageQuery.HotChocolate` — GraphQL target
- `PolarSharp.NaturalLanguageQuery.CustomerGraph` — CustomerGraphQuery target
- `PolarSharp.Reporting.GraphQL`, `PolarSharp.EcommerceStoreManagement.GraphQL` — underlying schemas

## License

MIT. (c) Molls and Hersh, LLC. 2026.
