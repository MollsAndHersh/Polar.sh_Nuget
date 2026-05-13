using Microsoft.EntityFrameworkCore;
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

    /// <summary>Benefit grants emitted by mirrored orders.</summary>
    public DbSet<ReportBenefitGrantEntity> BenefitGrants => Set<ReportBenefitGrantEntity>();

    /// <summary>Per-tenant per-resource snapshot checkpoints.</summary>
    public DbSet<ReportSnapshotCheckpointEntity> Checkpoints => Set<ReportSnapshotCheckpointEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PolarReportingDbContext).Assembly);
    }
}
