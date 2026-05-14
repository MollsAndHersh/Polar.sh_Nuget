using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;

namespace PolarSharp.Reporting.EntityFrameworkCore;

/// <summary>
/// EF Core DbContext for the local snapshot cache. Inherits from
/// <see cref="TenantAwareDbContextBase"/> so every snapshot table picks up the dual tenant +
/// fake-data global query filter automatically.
/// </summary>
public class PolarReportingDbContext : TenantAwareDbContextBase
{
    /// <summary>Initializes a new context.</summary>
    public PolarReportingDbContext(DbContextOptions<PolarReportingDbContext> options, IServiceProvider services)
        : base(options, services) { }

    /// <summary>Mirrored Polar event log.</summary>
    public DbSet<ReportEventEntity> Events => Set<ReportEventEntity>();

    /// <summary>Mirrored Polar orders with pre-aggregated columns.</summary>
    public DbSet<ReportOrderEntity> Orders => Set<ReportOrderEntity>();

    /// <summary>Line items belonging to mirrored orders.</summary>
    public DbSet<ReportOrderLineItemEntity> OrderLineItems => Set<ReportOrderLineItemEntity>();

    /// <summary>Refunds issued against mirrored orders.</summary>
    public DbSet<ReportOrderRefundEntity> OrderRefunds => Set<ReportOrderRefundEntity>();

    /// <summary>Mirrored Polar subscriptions.</summary>
    public DbSet<ReportSubscriptionEntity> Subscriptions => Set<ReportSubscriptionEntity>();

    /// <summary>Mirrored Polar customers with pre-aggregated columns.</summary>
    public DbSet<ReportCustomerEntity> Customers => Set<ReportCustomerEntity>();

    /// <summary>Benefit grants emitted by mirrored orders (and standalone via /v1/benefit-grants/).</summary>
    public DbSet<ReportBenefitGrantEntity> BenefitGrants => Set<ReportBenefitGrantEntity>();

    // ── V20-005 Phase 1: 7 new resource snapshots ───────────────────────────────

    /// <summary>Mirrored Polar benefit definitions (per-tenant catalog).</summary>
    public DbSet<ReportBenefitEntity> Benefits => Set<ReportBenefitEntity>();

    /// <summary>Mirrored Polar discount definitions.</summary>
    public DbSet<ReportDiscountEntity> Discounts => Set<ReportDiscountEntity>();

    /// <summary>Mirrored Polar checkout-link configurations.</summary>
    public DbSet<ReportCheckoutLinkEntity> CheckoutLinks => Set<ReportCheckoutLinkEntity>();

    /// <summary>Mirrored Polar product catalog (Polar-side state — distinct from the host's own catalog managed by PolarCatalogPublisher).</summary>
    public DbSet<ReportProductEntity> Products => Set<ReportProductEntity>();

    /// <summary>Mirrored Polar license keys (utilization + expiry pipeline reports).</summary>
    public DbSet<ReportLicenseKeyEntity> LicenseKeys => Set<ReportLicenseKeyEntity>();

    /// <summary>Mirrored Polar usage-billing meter definitions.</summary>
    public DbSet<ReportMeterEntity> Meters => Set<ReportMeterEntity>();

    /// <summary>Mirrored per-customer per-meter tallies.</summary>
    public DbSet<ReportCustomerMeterEntity> CustomerMeters => Set<ReportCustomerMeterEntity>();

    /// <summary>Per-tenant per-resource snapshot checkpoints.</summary>
    public DbSet<ReportSnapshotCheckpointEntity> Checkpoints => Set<ReportSnapshotCheckpointEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PolarReportingDbContext).Assembly);

        // SQLite doesn't support ORDER BY on DateTimeOffset columns (EF Core limitation —
        // SQLite stores them as TEXT and refuses ORDER BY translation). The drilldown
        // queries (ListCustomersAsync sort by LastOrderAt, ListOrdersForCustomerAsync sort
        // by CreatedAt) need server-side ordering, so we apply a value converter that
        // stores DateTimeOffset as long (UtcTicks) for SQLite ONLY. SqlServer + Postgres
        // both support DateTimeOffset natively in ORDER BY and keep the original column
        // type. Conversion is lossless — UtcTicks round-trips through DateTimeOffset.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            ApplySqliteDateTimeOffsetConverter(modelBuilder);
        }
    }

    private static void ApplySqliteDateTimeOffsetConverter(ModelBuilder modelBuilder)
    {
        var nonNullable = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        var nullable = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.UtcTicks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTimeOffset))
                    prop.SetValueConverter(nonNullable);
                else if (prop.ClrType == typeof(DateTimeOffset?))
                    prop.SetValueConverter(nullable);
            }
        }
    }
}
