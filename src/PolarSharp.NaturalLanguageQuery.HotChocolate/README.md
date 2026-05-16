# PolarSharp.NaturalLanguageQuery.HotChocolate

Hot Chocolate target for natural-language to GraphQL query generation.

## Install

```sh
dotnet add package PolarSharp.NaturalLanguageQuery.HotChocolate
```

## Quickstart

```csharp
// Inject IHotChocolateNaturalLanguageGenerator after registering the router + this target:
var request = new NaturalLanguageQueryRequest(
    NaturalLanguageInput: "subscriptions that churned last month, by plan",
    AudienceScope: AudienceScope.Tenant,
    TenantId: currentTenant.Id,
    UserId: currentUser.Id);
var response = await generator.GenerateAsync(request, ct);
// response.GeneratedQueryDocument is a runnable GraphQL document against /graphql/reporting.
```

## What this package does

Generates GraphQL query documents from natural-language input against the audience-scoped schema slices of `PolarSharp.Reporting.GraphQL` + `PolarSharp.EcommerceStoreManagement.GraphQL`. Per Case Study 04 "Audience-Scoped Schema Slicing":

- The schema slice is computed from caller's `AudienceScope` + actual permissions.
- The LLM receives ONLY the slice — it **literally cannot reference fields outside it** because those fields are not in the prompt.
- Structured-output enforcement (Anthropic tool-use OR OpenAI `response_format=json_schema`) constrains the LLM's output to a valid GraphQL document shape.
- The generated query is parsed; Hot Chocolate's request executor runs it in `validation-only` mode for dry-run authorization.
- A cost / complexity gate (depth, field count, RU estimate) executes before the query runs.

LLM client integration re-uses the v1.2 translation providers (`PolarSharp.EcommerceStoreManagement.Translation.*`) as `IAiCompletionClient` — same provider abstraction the catalog translation pipeline uses, so hosts who already chose Anthropic, OpenAI, Azure OpenAI, Gemini, or Grok for translation get NL query support for free.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 19.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 04: Audience-Scoped Schema Slicing](../../Case%20Studies/04-Audience-Scoped-Schema-Slicing.md)
- `PolarSharp.NaturalLanguageQuery` — the router + request/response types
- `PolarSharp.NaturalLanguageQuery.CustomerGraph` — graph-DSL alternative target
- `PolarSharp.Reporting.GraphQL`, `PolarSharp.EcommerceStoreManagement.GraphQL` — underlying schemas

## License

MIT. (c) Molls and Hersh, LLC. 2026.
