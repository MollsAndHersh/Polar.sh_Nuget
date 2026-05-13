using Microsoft.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// The EF Core <see cref="DbContext"/> that backs the SQL-based tenant registry.
/// </summary>
/// <remarks>
/// <para>
/// Holds the <see cref="PolarTenantInfoEntity"/> rows that PolarSharp resolves on every
/// request via Finbuckle's tenant store interface. Unlike catalog / identity / reporting
/// DbContexts, this context is NOT tenant-scoped: it IS the tenant registry, so it must be
/// readable across tenants by the bootstrap lookup. Application-level filtering happens at
/// the consuming layer (<c>EfMultiTenantStore</c>), not via global query filter.
/// </para>
/// <para>
/// Provider-specific subclasses (<c>SqlServerPolarTenantDbContext</c>, etc.) supply the
/// concrete connection string and migrations. The <see cref="OnModelCreating"/> method below
/// applies the core relational schema; providers may extend with provider-specific
/// configuration (e.g., RLS DDL is part of the migrations, not the model).
/// </para>
/// </remarks>
public class PolarTenantDbContext : DbContext
{
    /// <summary>Initializes a new <see cref="PolarTenantDbContext"/> with the supplied options.</summary>
    /// <param name="options">EF Core options including provider and connection string.</param>
    public PolarTenantDbContext(DbContextOptions<PolarTenantDbContext> options) : base(options) { }

    /// <summary>Gets the tenant registry table.</summary>
    public DbSet<PolarTenantInfoEntity> Tenants => Set<PolarTenantInfoEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PolarTenantInfoEntity>(e =>
        {
            e.ToTable("polar_tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Identifier).IsUnique();
            e.Property(x => x.Identifier).HasMaxLength(128).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.PolarAccessToken).HasMaxLength(512).IsRequired();
            e.Property(x => x.WebhookEndpointId).HasMaxLength(128);
            e.Property(x => x.WebhookSecret).HasMaxLength(256);
            e.Property(x => x.Server).HasConversion<string>().HasMaxLength(32);
        });
    }
}
