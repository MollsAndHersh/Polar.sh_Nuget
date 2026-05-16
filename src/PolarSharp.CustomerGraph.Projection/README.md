# PolarSharp.CustomerGraph.Projection

Background projection IHostedService that feeds Polar webhook + EF SaveChanges events into the customer graph.

## Install

```sh
dotnet add package PolarSharp.CustomerGraph.Projection
```

## Quickstart

```csharp
// After registering a graph provider (e.g. AddPolarCustomerGraphNeo4j(...)):
builder.Services.AddHostedService<CustomerGraphProjectionHostedService>();
// Producers push GraphProjectionEvent items into the bounded Channel<GraphProjectionEvent>;
// the hosted service drains the channel and applies events via ICustomerGraphProjector.
```

## What this package does

Hosts the projection pipeline that keeps the customer graph fresh. Source events feed in from three layers:

- **Polar webhooks** — `order.created`, `order.paid`, `refund.completed`, `customer.updated`, etc.
- **EF Core `SaveChangesInterceptor`** on `EcommerceStoreManagement` DbContexts.
- **IP capture events** when the tenant has `IpCaptureMode != Disabled`.

The pipeline uses a bounded `Channel<GraphProjectionEvent>` (default 10,000 capacity per tenant) so producer backpressure kicks in if the projection falls behind. Failures retry with exponential backoff; after 3 retries the event dead-letters to `PlatformAuditLogEntry`. The five event shapes — `CustomerUpsertedEvent`, `PurchaseRecordedEvent`, `IpUsageRecordedEvent`, `TagsUpdatedEvent`, `CustomerErasedEvent` — match the typed projector surface. See Case Study 01 "Lift-And-Shift Architecture" for the provider-swap posture.

## Status

v1.3.0 (scaffold + abstractions shipped; full implementation lands in Phase 17.x patches per the v1.3 plan).

## See also

- [PolarSharp v1.3.0 plan](/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md) (full architectural specification)
- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- `PolarSharp.CustomerGraph` — the typed query abstraction
- `PolarSharp.CustomerGraph.Neo4j` — Neo4j provider
- `PolarSharp.Webhooks` — Polar webhook receiver feeding events into the channel

## License

MIT. (c) Molls and Hersh, LLC. 2026.
