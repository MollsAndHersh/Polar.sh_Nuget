# PolarSharp.EcommerceStoreManagement

Local-first catalog authoring with **publish-to-Polar** workflow. Author products, services, categories, tiered packages, variants, benefits, discounts, and checkout links in a local per-tenant SQL store; publish the entire (or delta) catalog to Polar.sh on demand. Includes refund management, admin audit log, license key validation, and inventory auto-sync.

## Install

Pick a provider:

```bash
dotnet add package PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer
# or .Sqlite, or .PostgreSQL
```

## Quickstart

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarMultiTenant().UseSqlServer(connStr)
    .AddPolarIdentity().UseSqlServer(connStr)
    .AddPolarEcommerce().UseSqlServer(connStr);
```

## Features

| Capability | Type / API |
|---|---|
| Products, variants, categories | `LocalProduct`, `LocalProductVariant`, `LocalCategory` |
| Tier packages (Basic / Advanced / Ultimate) | `LocalTierGroup` + `TierLevel` with cumulative benefit inheritance |
| Subscriptions | `LocalPrice.IsRecurring` + trial periods |
| Benefits | Sealed-record hierarchy: `LicenseKeysBenefit`, `DownloadablesBenefit`, `GitHubRepoBenefit`, `DiscordRoleBenefit`, `FeatureFlagBenefit`, `MeterCreditBenefit`, `CustomBenefit` |
| Discounts + coupon codes | `LocalDiscount` |
| Checkout customization | `LocalCheckoutLinkConfig` |
| Business profile + banking | `TenantBusinessProfile` + `IPolarBusinessProfileService` (Stripe Connect via dashboard deep-link) |
| Refunds | `IRefundService` (full + partial) |
| Audit log | `AdminAuditLogEntry` via `SaveChangesInterceptor` |
| License validation | `ILicenseKeyValidator` + `[RequireValidLicense]` MVC filter |
| Inventory auto-sync | `IInventoryUpdater` + zero-boundary `is_archived` PATCH |
| Multi-language descriptions | `IPolarCatalogTranslator` abstraction (default no-op; install Translation.* package for AI provider) |

See `docs/articles/catalog-authoring.md` and `docs/articles/catalog-publish.md` on the [GitHub Pages site](https://mollsandhersh.github.io/Polar.sh_Nuget/).

## License

MIT.
