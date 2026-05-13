using Microsoft.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.Translation;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;

/// <summary>
/// EF Core DbContext for the local catalog. Inherits from
/// <see cref="TenantAwareDbContextBase"/> so every <c>ITenantOwned</c> table picks up the
/// dual tenant + fake-data global query filter from Phase 2 automatically.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Schema:</strong> the model is built from <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// classes discovered in the same assembly. Each entity has one configuration class
/// (<c>Configurations/XxxConfiguration.cs</c>) and there are NO data annotations on the
/// entity classes — keeping persistence concerns separate from the domain shape.
/// </para>
/// </remarks>
public class PolarCatalogDbContext : TenantAwareDbContextBase
{
    /// <summary>Initializes a new <see cref="PolarCatalogDbContext"/>.</summary>
    /// <param name="options">EF options (provider, connection string).</param>
    /// <param name="services">The current scope — used to resolve tenant context.</param>
    public PolarCatalogDbContext(DbContextOptions<PolarCatalogDbContext> options, IServiceProvider services)
        : base(options, services) { }

    /// <summary>Tenants' business profiles.</summary>
    public DbSet<Entities.TenantBusinessProfileEntity> BusinessProfiles => Set<Entities.TenantBusinessProfileEntity>();

    /// <summary>Local products and services.</summary>
    public DbSet<Entities.LocalProductEntity> Products => Set<Entities.LocalProductEntity>();

    /// <summary>Variants belonging to products.</summary>
    public DbSet<Entities.LocalProductVariantEntity> Variants => Set<Entities.LocalProductVariantEntity>();

    /// <summary>M:N join — products ↔ categories.</summary>
    public DbSet<Entities.LocalProductCategoryAssignmentEntity> ProductCategories => Set<Entities.LocalProductCategoryAssignmentEntity>();

    /// <summary>Categories.</summary>
    public DbSet<Entities.LocalCategoryEntity> Categories => Set<Entities.LocalCategoryEntity>();

    /// <summary>Departments.</summary>
    public DbSet<Entities.LocalDepartmentEntity> Departments => Set<Entities.LocalDepartmentEntity>();

    /// <summary>Tier groups + the embedded tier levels (owned).</summary>
    public DbSet<Entities.LocalTierGroupEntity> TierGroups => Set<Entities.LocalTierGroupEntity>();

    /// <summary>Local benefit definitions.</summary>
    public DbSet<Entities.LocalBenefitEntity> Benefits => Set<Entities.LocalBenefitEntity>();

    /// <summary>Local discount definitions.</summary>
    public DbSet<Entities.LocalDiscountEntity> Discounts => Set<Entities.LocalDiscountEntity>();

    /// <summary>Checkout-link configurations.</summary>
    public DbSet<Entities.LocalCheckoutLinkEntity> CheckoutLinks => Set<Entities.LocalCheckoutLinkEntity>();

    /// <summary>The admin audit log.</summary>
    public DbSet<AdminAuditLogEntry> AuditLog => Set<AdminAuditLogEntry>();

    /// <summary>The translation rows (master values stay in the source entity row; non-master translations live here).</summary>
    public DbSet<CatalogTranslationEntity> Translations => Set<CatalogTranslationEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PolarCatalogDbContext).Assembly);
    }
}
