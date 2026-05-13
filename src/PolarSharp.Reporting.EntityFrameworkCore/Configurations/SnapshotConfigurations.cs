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
