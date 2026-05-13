# PolarSharp.EcommerceStoreManagement.EntityFrameworkCore

EF Core base for catalog persistence. Provider-agnostic — install one of `.SqlServer`, `.Sqlite`, or `.PostgreSQL`.

Provides `PolarCatalogDbContext` (extending `TenantAwareDbContextBase`), entity mappings for every `LocalProduct` / `LocalBenefit` / `LocalDiscount` / `LocalCheckoutLinkConfig` / `TenantBusinessProfile` / `AdminAuditLogEntry`, and `EfCatalogRepository` implementations of the catalog repository abstractions.

## License

MIT.
