using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Configurations;

/// <summary>Schema config for the business profile table.</summary>
internal sealed class TenantBusinessProfileConfiguration : IEntityTypeConfiguration<TenantBusinessProfileEntity>
{
    public void Configure(EntityTypeBuilder<TenantBusinessProfileEntity> b)
    {
        b.ToTable("polar_business_profiles");
        b.HasKey(e => e.TenantId);
        b.Property(e => e.TenantId).HasMaxLength(64);
        b.Property(e => e.OrganizationName).HasMaxLength(256).IsRequired();
        b.Property(e => e.CountryCode).HasMaxLength(2).IsRequired();
        b.Property(e => e.DefaultCurrency).HasMaxLength(3).IsRequired();
        b.Property(e => e.TaxBehavior).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.PayoutStatus).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.TranslationProvider).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.StreetLine1).HasMaxLength(256);
        b.Property(e => e.StreetLine2).HasMaxLength(256);
        b.Property(e => e.City).HasMaxLength(128);
        b.Property(e => e.StateOrProvince).HasMaxLength(64);
        b.Property(e => e.PostalCode).HasMaxLength(32);
        b.Property(e => e.MasterLanguage).HasMaxLength(16);
    }
}

/// <summary>Schema config for the catalog products table.</summary>
internal sealed class LocalProductConfiguration : IEntityTypeConfiguration<LocalProductEntity>
{
    public void Configure(EntityTypeBuilder<LocalProductEntity> b)
    {
        b.ToTable("polar_local_products");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.MasterName });
        b.HasIndex(e => new { e.TenantId, e.PolarProductId });
        b.HasIndex(e => new { e.TenantId, e.IsFakeData });
        b.Property(e => e.MasterName).HasMaxLength(256).IsRequired();
        b.Property(e => e.MasterLanguage).HasMaxLength(16);
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.PolarProductId).HasMaxLength(64);
    }
}

/// <summary>Schema config for the product↔category M:N join table.</summary>
internal sealed class LocalProductCategoryAssignmentConfiguration : IEntityTypeConfiguration<LocalProductCategoryAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<LocalProductCategoryAssignmentEntity> b)
    {
        b.ToTable("polar_local_product_categories");
        b.HasKey(e => e.Id);
        // Each (product, category) pair appears at most once.
        b.HasIndex(e => new { e.ProductId, e.CategoryId }).IsUnique();
        // Indexes for "products in category" and "categories of product" queries.
        b.HasIndex(e => new { e.TenantId, e.CategoryId });
        b.HasIndex(e => new { e.TenantId, e.ProductId });
    }
}

/// <summary>Schema config for the variants table.</summary>
internal sealed class LocalProductVariantConfiguration : IEntityTypeConfiguration<LocalProductVariantEntity>
{
    public void Configure(EntityTypeBuilder<LocalProductVariantEntity> b)
    {
        b.ToTable("polar_local_variants");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.ProductId });
        b.Property(e => e.Sku).HasMaxLength(64);
        b.Property(e => e.PolarProductId).HasMaxLength(64);
    }
}

/// <summary>Schema config for the categories table.</summary>
internal sealed class LocalCategoryConfiguration : IEntityTypeConfiguration<LocalCategoryEntity>
{
    public void Configure(EntityTypeBuilder<LocalCategoryEntity> b)
    {
        b.ToTable("polar_local_categories");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.ParentCategoryId });
        b.Property(e => e.MasterName).HasMaxLength(128).IsRequired();
        b.Property(e => e.Description).HasMaxLength(1024);
    }
}

/// <summary>Schema config for the departments table.</summary>
internal sealed class LocalDepartmentConfiguration : IEntityTypeConfiguration<LocalDepartmentEntity>
{
    public void Configure(EntityTypeBuilder<LocalDepartmentEntity> b)
    {
        b.ToTable("polar_local_departments");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.MasterName });
        b.Property(e => e.MasterName).HasMaxLength(128).IsRequired();
        b.Property(e => e.Description).HasMaxLength(1024);
    }
}

/// <summary>Schema config for the tier-groups table.</summary>
internal sealed class LocalTierGroupConfiguration : IEntityTypeConfiguration<LocalTierGroupEntity>
{
    public void Configure(EntityTypeBuilder<LocalTierGroupEntity> b)
    {
        b.ToTable("polar_local_tier_groups");
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(128).IsRequired();
    }
}

/// <summary>Schema config for the benefits table.</summary>
internal sealed class LocalBenefitConfiguration : IEntityTypeConfiguration<LocalBenefitEntity>
{
    public void Configure(EntityTypeBuilder<LocalBenefitEntity> b)
    {
        b.ToTable("polar_local_benefits");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.BenefitKind });
        b.Property(e => e.BenefitKind).HasMaxLength(32).IsRequired();
        b.Property(e => e.Name).HasMaxLength(256).IsRequired();
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
    }
}

/// <summary>Schema config for the discounts table.</summary>
internal sealed class LocalDiscountConfiguration : IEntityTypeConfiguration<LocalDiscountEntity>
{
    public void Configure(EntityTypeBuilder<LocalDiscountEntity> b)
    {
        b.ToTable("polar_local_discounts");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        b.Property(e => e.Code).HasMaxLength(64);
        b.Property(e => e.MasterName).HasMaxLength(256).IsRequired();
        b.Property(e => e.Type).HasMaxLength(16).IsRequired();
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.DurationKind).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
    }
}

/// <summary>Schema config for the checkout-links table.</summary>
internal sealed class LocalCheckoutLinkConfiguration : IEntityTypeConfiguration<LocalCheckoutLinkEntity>
{
    public void Configure(EntityTypeBuilder<LocalCheckoutLinkEntity> b)
    {
        b.ToTable("polar_local_checkout_links");
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(128).IsRequired();
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
    }
}

/// <summary>Schema config for the admin audit log table.</summary>
internal sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AdminAuditLogEntry> b)
    {
        b.ToTable("polar_admin_audit_log");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.OccurredAt });
        b.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
        b.Property(e => e.TenantId).IsRequired().HasMaxLength(64);
        b.Property(e => e.ActorEmail).HasMaxLength(320).IsRequired();
        b.Property(e => e.EntityType).HasMaxLength(128).IsRequired();
        b.Property(e => e.Action).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.CrossTenantJustification).HasMaxLength(2048);
        b.Ignore(e => e.TenantGuid);   // computed property; not a column
        // JsonNode columns serialised by hand to/from string — EF can't map JsonNode directly.
        // No explicit column type so EF picks provider-appropriate large-text storage:
        // nvarchar(max) on SQL Server, TEXT on SQLite, text on PostgreSQL.
        b.Property(e => e.BeforeValues)
            .HasConversion(
                v => v == null ? null : v.ToJsonString(),
                v => v == null ? null : System.Text.Json.Nodes.JsonNode.Parse(v));
        b.Property(e => e.AfterValues)
            .HasConversion(
                v => v == null ? null : v.ToJsonString(),
                v => v == null ? null : System.Text.Json.Nodes.JsonNode.Parse(v));
        b.Property(e => e.ChangedFields)
            .HasConversion(
                v => string.Join(',', v),
                v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>Schema config for the catalog_translations table.</summary>
internal sealed class CatalogTranslationConfiguration : IEntityTypeConfiguration<CatalogTranslationEntity>
{
    public void Configure(EntityTypeBuilder<CatalogTranslationEntity> b)
    {
        b.ToTable("catalog_translations");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId, e.Language, e.FieldName }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
        b.Property(e => e.TenantId).HasMaxLength(64).IsRequired();
        b.Property(e => e.EntityType).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Language).HasMaxLength(16).IsRequired();
        b.Property(e => e.FieldName).HasMaxLength(64).IsRequired();
        b.Property(e => e.SourceProvider).HasMaxLength(16);
        b.Property(e => e.SourceModel).HasMaxLength(64);
    }
}
