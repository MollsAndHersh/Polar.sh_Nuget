# PolarSharp.DataSeeding

Bulk realistic-fake-data seeder for sandbox / QA / demo environments. Uses `Bogus` to generate plausible customers, products with variants, categories, tier groups, benefits, discounts, checkout links, subscriptions, and orders. Every row is tagged `IsFakeData=true`; flipping `TenantBusinessProfile.AllowFakeData` triggers automatic sync to/from Polar's sandbox.

## Install

```bash
dotnet add package PolarSharp.DataSeeding
```

## Quickstart

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServer(connStr)
    .AddPolarDataSeeding();

// Then in a controller / endpoint:
await seeder.SeedFullCatalogAsync(tenantId, SeedScale.QA);
```

`SeedScale` presets: `Demo` (tiny — 10 customers, 20 products), `QA` (medium — 200/500), `Stress` (large — 10000/50000).

Pass an optional `randomSeed: 42` for deterministic CI reproducibility.

When `TenantBusinessProfile.AllowFakeData=false`, every read query, report, and publish silently excludes fake records via the dual EF global query filter — no caller code changes needed.

See `docs/articles/data-seeding.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/).

## License

MIT.
