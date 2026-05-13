# Data Seeding

`PolarSharp.DataSeeding` is an optional, **dev-time** package that bulk-generates realistic fake catalog data (customers, products with variants, categories, tier groups, benefits, discounts, checkout links) for sandbox / QA / demo environments. Every row produced is tagged `IsFakeData = true`; flipping `TenantBusinessProfile.AllowFakeData` triggers the bundled `FakeDataSyncService` to sync to / from Polar's sandbox.

## Install

```sh
dotnet add package PolarSharp.DataSeeding
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServerCatalog(connStr)
    .AddPolarDataSeeding(builder.Configuration);

// Later, in an admin endpoint:
await seeder.SeedFullCatalogAsync(tenantId, SeedScale.QA, randomSeed: 42);
```

## `SeedScale` presets

| Scale | Customers | Products | Categories | Use for |
|---|---|---|---|---|
| `Demo` | 10 | 20 | 5 | Screencasts, sales demos |
| `QA` | 200 | 500 | 30 | Pre-release testing, integration smoke |
| `Stress` | 10,000 | 50,000 | 200 | Load testing, index-tuning verification |

## Deterministic seeding

Pass a fixed `randomSeed` to reproduce byte-identical output across runs — essential for CI fixtures:

```csharp
await seeder.SeedProductsAsync(tenantId, count: 25, randomSeed: 1);   // run A and run B emit the same 25 products
```

## Toggle-driven Polar sandbox sync

When `TenantBusinessProfile.AllowFakeData` changes:

- **OFF → ON:** every `IsFakeData = true` local record is published to Polar's sandbox so the merchant can exercise the full purchase flow against representative data.
- **ON → OFF:** previously-published fake records are archived in Polar; locally, the dual EF global query filter silently hides them from every read query, report, and publish — no caller code changes.

The host publishes the change via `IFakeDataToggleNotifier.Notify(...)`; the `FakeDataSyncService` background service consumes and reconciles.

## Developer note: AOT publish

The bundled `PolarTestApp` does **not** transitively reference `PolarSharp.DataSeeding`, so `dotnet publish -p:PublishAot=true` against the test app stays clean. The `Bogus` faker library used by `PolarSharp.DataSeeding` does some reflection internally — hosts who publish AOT with `PolarSharp.DataSeeding` installed may see reflection / trim warnings. Two supported mitigations:

**Option 1 — suppress the warnings in the host's csproj:**

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="Bogus" />
</ItemGroup>
```

**Option 2 — gate registration behind `#if DEBUG`** so the package compiles out of the Production build entirely:

```csharp
#if DEBUG
builder.Services.AddPolarDataSeeding(builder.Configuration);
#endif
```

`PolarSharp.DataSeeding` is a dev-time package — designed for sandbox / QA / demo environments, not production hot paths — so either approach is acceptable.

## Custom generators

`IFakeGenerator<T>` is the per-entity contract. To add a custom generator (e.g., a host-specific benefit kind):

```csharp
public sealed class FakeDiscordRoleBenefitGenerator : IFakeGenerator<DiscordRoleBenefit>
{
    public DiscordRoleBenefit Generate(string tenantId, Faker faker) => new()
    {
        // ... populate via Bogus
        IsFakeData = true,
    };
}

builder.Services.AddSingleton<FakeDiscordRoleBenefitGenerator>();
```

## Custom sinks

By default `AddPolarDataSeeding(...)` registers a `CountingNoOpSeedSink` — useful for tests where you want to inspect the generated records. Hosts wire a real sink to persist to their catalog DbContext:

```csharp
builder.Services.AddScoped<ISeedSink, MyCatalogSeedSink>();
```
