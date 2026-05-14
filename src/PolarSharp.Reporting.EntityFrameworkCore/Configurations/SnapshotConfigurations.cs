using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;

namespace PolarSharp.Reporting.EntityFrameworkCore.Configurations;

internal sealed class ReportEventConfig : IEntityTypeConfiguration<ReportEventEntity>
{
    public void Configure(EntityTypeBuilder<ReportEventEntity> b)
    {
        b.ToTable("polar_report_events");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.OccurredAt });
        b.HasIndex(e => new { e.TenantId, e.PolarEventId }).IsUnique();
        b.Property(e => e.PolarEventId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Type).HasMaxLength(64).IsRequired();
    }
}

internal sealed class ReportOrderConfig : IEntityTypeConfiguration<ReportOrderEntity>
{
    public void Configure(EntityTypeBuilder<ReportOrderEntity> b)
    {
        b.ToTable("polar_report_orders");
        b.HasKey(o => o.Id);
        // Powers ListOrdersForCustomerAsync paging.
        b.HasIndex(o => new { o.TenantId, o.CustomerId, o.CreatedAt });
        b.HasIndex(o => new { o.TenantId, o.PolarOrderId }).IsUnique();
        b.HasIndex(o => new { o.TenantId, o.CreatedAt });
        b.Property(o => o.PolarOrderId).HasMaxLength(64).IsRequired();
        b.Property(o => o.OrderNumber).HasMaxLength(64).IsRequired();
        b.Property(o => o.CustomerId).HasMaxLength(64).IsRequired();
        b.Property(o => o.Status).HasMaxLength(32).IsRequired();
        b.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        b.Property(o => o.InvoiceUrl).HasMaxLength(2048);
    }
}

internal sealed class ReportOrderLineItemConfig : IEntityTypeConfiguration<ReportOrderLineItemEntity>
{
    public void Configure(EntityTypeBuilder<ReportOrderLineItemEntity> b)
    {
        b.ToTable("polar_report_order_line_items");
        b.HasKey(li => li.Id);
        b.HasIndex(li => new { li.TenantId, li.OrderId });
        b.Property(li => li.ProductId).HasMaxLength(64).IsRequired();
        b.Property(li => li.ProductName).HasMaxLength(256).IsRequired();
        b.Property(li => li.PriceId).HasMaxLength(64);
    }
}

internal sealed class ReportOrderRefundConfig : IEntityTypeConfiguration<ReportOrderRefundEntity>
{
    public void Configure(EntityTypeBuilder<ReportOrderRefundEntity> b)
    {
        b.ToTable("polar_report_order_refunds");
        b.HasKey(r => r.Id);
        b.HasIndex(r => new { r.TenantId, r.OrderId });
        b.HasIndex(r => new { r.TenantId, r.PolarRefundId }).IsUnique();
        b.Property(r => r.PolarRefundId).HasMaxLength(64).IsRequired();
        b.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        b.Property(r => r.Reason).HasMaxLength(32).IsRequired();
    }
}

internal sealed class ReportSubscriptionConfig : IEntityTypeConfiguration<ReportSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<ReportSubscriptionEntity> b)
    {
        b.ToTable("polar_report_subscriptions");
        b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.TenantId, s.PolarSubscriptionId }).IsUnique();
        b.HasIndex(s => new { s.TenantId, s.Status, s.StartedAt });
        b.Property(s => s.PolarSubscriptionId).HasMaxLength(64).IsRequired();
        b.Property(s => s.CustomerId).HasMaxLength(64).IsRequired();
        b.Property(s => s.ProductId).HasMaxLength(64).IsRequired();
        b.Property(s => s.Status).HasMaxLength(32).IsRequired();
    }
}

internal sealed class ReportCustomerConfig : IEntityTypeConfiguration<ReportCustomerEntity>
{
    public void Configure(EntityTypeBuilder<ReportCustomerEntity> b)
    {
        b.ToTable("polar_report_customers");
        b.HasKey(c => c.Id);
        b.HasIndex(c => new { c.TenantId, c.PolarCustomerId }).IsUnique();
        // Powers ListCustomersAsync paging with default LastOrderAt-desc sort.
        b.HasIndex(c => new { c.TenantId, c.LastOrderAt });
        b.HasIndex(c => new { c.TenantId, c.Email });
        b.Property(c => c.PolarCustomerId).HasMaxLength(64).IsRequired();
        b.Property(c => c.Email).HasMaxLength(320).IsRequired();
        b.Property(c => c.Name).HasMaxLength(256);
        b.Property(c => c.Currency).HasMaxLength(3).IsRequired();
    }
}

internal sealed class ReportBenefitGrantConfig : IEntityTypeConfiguration<ReportBenefitGrantEntity>
{
    public void Configure(EntityTypeBuilder<ReportBenefitGrantEntity> b)
    {
        b.ToTable("polar_report_benefit_grants");
        b.HasKey(g => g.Id);
        b.HasIndex(g => new { g.TenantId, g.PolarGrantId }).IsUnique();
        b.HasIndex(g => new { g.TenantId, g.OrderId });
        b.HasIndex(g => new { g.TenantId, g.CustomerId, g.IsGranted });
        b.Property(g => g.PolarGrantId).HasMaxLength(64).IsRequired();
        b.Property(g => g.CustomerId).HasMaxLength(64).IsRequired();
        b.Property(g => g.BenefitId).HasMaxLength(64).IsRequired();
        b.Property(g => g.BenefitName).HasMaxLength(256).IsRequired();
        b.Property(g => g.BenefitKind).HasMaxLength(32).IsRequired();
    }
}

// ── V20-005 Phase 1: configurations for the 7 new resource snapshots ──────────────

internal sealed class ReportBenefitConfig : IEntityTypeConfiguration<ReportBenefitEntity>
{
    public void Configure(EntityTypeBuilder<ReportBenefitEntity> b)
    {
        b.ToTable("polar_report_benefits");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.PolarBenefitId }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.Kind, e.IsActive });
        b.Property(e => e.PolarBenefitId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Name).HasMaxLength(256).IsRequired();
        b.Property(e => e.Kind).HasMaxLength(32).IsRequired();
        b.Property(e => e.Description).HasMaxLength(1024);
    }
}

internal sealed class ReportDiscountConfig : IEntityTypeConfiguration<ReportDiscountEntity>
{
    public void Configure(EntityTypeBuilder<ReportDiscountEntity> b)
    {
        b.ToTable("polar_report_discounts");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.PolarDiscountId }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.Code });
        b.HasIndex(e => new { e.TenantId, e.EndsAt });
        b.Property(e => e.PolarDiscountId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Name).HasMaxLength(256).IsRequired();
        b.Property(e => e.Code).HasMaxLength(64);
        b.Property(e => e.Type).HasMaxLength(16).IsRequired();
        b.Property(e => e.Currency).HasMaxLength(3);
        b.Property(e => e.PercentOff).HasPrecision(5, 2);
    }
}

internal sealed class ReportCheckoutLinkConfig : IEntityTypeConfiguration<ReportCheckoutLinkEntity>
{
    public void Configure(EntityTypeBuilder<ReportCheckoutLinkEntity> b)
    {
        b.ToTable("polar_report_checkout_links");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.PolarCheckoutLinkId }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.CreatedAt });
        b.Property(e => e.PolarCheckoutLinkId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Label).HasMaxLength(256).IsRequired();
        b.Property(e => e.ProductIdsCsv).HasMaxLength(2048).IsRequired();
        b.Property(e => e.Url).HasMaxLength(2048);
        b.Property(e => e.SuccessUrl).HasMaxLength(2048);
    }
}

internal sealed class ReportProductConfig : IEntityTypeConfiguration<ReportProductEntity>
{
    public void Configure(EntityTypeBuilder<ReportProductEntity> b)
    {
        b.ToTable("polar_report_products");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.PolarProductId }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.IsArchived });
        b.HasIndex(e => new { e.TenantId, e.IsRecurring });
        b.Property(e => e.PolarProductId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Name).HasMaxLength(256).IsRequired();
        b.Property(e => e.Description).HasMaxLength(2048);
        b.Property(e => e.RecurringInterval).HasMaxLength(16);
    }
}

internal sealed class ReportLicenseKeyConfig : IEntityTypeConfiguration<ReportLicenseKeyEntity>
{
    public void Configure(EntityTypeBuilder<ReportLicenseKeyEntity> b)
    {
        b.ToTable("polar_report_license_keys");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.PolarLicenseKeyId }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.CustomerId });
        b.HasIndex(e => new { e.TenantId, e.Status, e.ExpiresAt });
        b.Property(e => e.PolarLicenseKeyId).HasMaxLength(64).IsRequired();
        b.Property(e => e.CustomerId).HasMaxLength(64).IsRequired();
        b.Property(e => e.BenefitId).HasMaxLength(64);
        b.Property(e => e.DisplayKey).HasMaxLength(128);
        b.Property(e => e.Status).HasMaxLength(16).IsRequired();
    }
}

internal sealed class ReportMeterConfig : IEntityTypeConfiguration<ReportMeterEntity>
{
    public void Configure(EntityTypeBuilder<ReportMeterEntity> b)
    {
        b.ToTable("polar_report_meters");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.PolarMeterId }).IsUnique();
        b.Property(e => e.PolarMeterId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Name).HasMaxLength(256).IsRequired();
        b.Property(e => e.AggregationKind).HasMaxLength(32).IsRequired();
    }
}

internal sealed class ReportCustomerMeterConfig : IEntityTypeConfiguration<ReportCustomerMeterEntity>
{
    public void Configure(EntityTypeBuilder<ReportCustomerMeterEntity> b)
    {
        b.ToTable("polar_report_customer_meters");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.PolarCustomerMeterId }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.CustomerId, e.MeterId });
        b.Property(e => e.PolarCustomerMeterId).HasMaxLength(64).IsRequired();
        b.Property(e => e.CustomerId).HasMaxLength(64).IsRequired();
        b.Property(e => e.MeterId).HasMaxLength(64).IsRequired();
        b.Property(e => e.ConsumedUnits).HasPrecision(20, 4);
        b.Property(e => e.CreditedUnits).HasPrecision(20, 4);
    }
}

internal sealed class ReportSnapshotCheckpointConfig : IEntityTypeConfiguration<ReportSnapshotCheckpointEntity>
{
    public void Configure(EntityTypeBuilder<ReportSnapshotCheckpointEntity> b)
    {
        b.ToTable("polar_report_snapshot_checkpoints");
        b.HasKey(c => new { c.TenantId, c.Resource });
        b.Property(c => c.TenantId).HasMaxLength(64);
        b.Property(c => c.Resource).HasMaxLength(32);
        b.Property(c => c.LastPolarId).HasMaxLength(64);
    }
}
