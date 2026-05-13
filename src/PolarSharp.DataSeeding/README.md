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

## Developer note: AOT publish

The bundled `PolarTestApp` does **not** transitively reference `PolarSharp.DataSeeding`, so `dotnet publish -p:PublishAot=true` against the test app stays clean. The `Bogus` faker library uses some reflection internally — hosts who publish AOT with `PolarSharp.DataSeeding` installed may see reflection / trim warnings. Two supported mitigations:

1. Suppress the warnings only in the host's csproj:

   ```xml
   <ItemGroup>
     <TrimmerRootAssembly Include="Bogus" />
   </ItemGroup>
   ```

2. Gate the `AddPolarDataSeeding(...)` registration behind `#if DEBUG` so the package compiles out of the Production build entirely:

   ```csharp
   #if DEBUG
   builder.Services.AddPolarDataSeeding(builder.Configuration);
   #endif
   ```

`PolarSharp.DataSeeding` is a dev-time package — designed for sandbox / QA / demo environments, not production hot paths — so either approach is acceptable.

## License

MIT.
