# PolarSharp.Reporting.EntityFrameworkCore

EF Core base for the optional local snapshot cache. Provider-agnostic — install one of `.SqlServer`, `.Sqlite`, or `.PostgreSQL`.

Provides `PolarReportingDbContext` (extending `TenantAwareDbContextBase`), snapshot entity mappings (`ReportEventEntity`, `ReportOrderEntity`, `ReportSubscriptionEntity`, `ReportCustomerEntity`, `ReportSnapshotCheckpointEntity`), and `PlatformAuditLogEntity` (non-tenant-scoped for AppMasterAdmin oversight).

## License

MIT.
