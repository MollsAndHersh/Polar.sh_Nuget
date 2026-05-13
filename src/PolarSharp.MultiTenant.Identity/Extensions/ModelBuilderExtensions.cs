using Microsoft.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.Identity.Extensions;

/// <summary>
/// EF Core <see cref="ModelBuilder"/> extensions that let a host's own
/// <see cref="DbContext"/> participate in PolarSharp Identity by hosting the
/// <see cref="PolarUserTenantMembership"/> and <see cref="PlatformAuditLogEntry"/> tables
/// (and the standard ASP.NET Core Identity tables) alongside the host's own entities.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds the PolarSharp Identity table mappings (memberships, platform audit log) to the
    /// supplied <paramref name="modelBuilder"/>.
    /// </summary>
    /// <param name="modelBuilder">The host DbContext's model builder.</param>
    /// <remarks>
    /// <para>
    /// Use this from a host DbContext that already inherits from
    /// <c>IdentityDbContext&lt;PolarApplicationUser, PolarApplicationRole, Guid&gt;</c> when
    /// you want PolarSharp's M:N memberships and platform audit log to live in the host's
    /// existing database without registering a separate <see cref="PolarUserDbContext"/>.
    /// </para>
    /// <para>
    /// <strong>Caller responsibilities:</strong> the host DbContext must call
    /// <c>base.OnModelCreating(modelBuilder)</c> first (to install the Identity table
    /// configurations from <see cref="Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext{TUser, TRole, TKey}"/>),
    /// then call this method to layer the PolarSharp tables on top.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class ApplicationDbContext : IdentityDbContext&lt;PolarApplicationUser, PolarApplicationRole, Guid&gt;
    /// {
    ///     public DbSet&lt;Order&gt; Orders =&gt; Set&lt;Order&gt;();   // host's own entity
    ///
    ///     protected override void OnModelCreating(ModelBuilder mb)
    ///     {
    ///         base.OnModelCreating(mb);
    ///         mb.AddPolarIdentitySchema();    // adds memberships + platform audit log
    ///         // host's own configurations follow...
    ///     }
    /// }
    /// </code>
    /// </example>
    public static ModelBuilder AddPolarIdentitySchema(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<PolarApplicationUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(256);
            e.Property(u => u.IsAppMasterAdmin).HasDefaultValue(false);
            e.HasMany(u => u.Memberships).WithOne(m => m.User!).HasForeignKey(m => m.UserId);
        });

        modelBuilder.Entity<PolarApplicationRole>(e =>
        {
            e.Property(r => r.Description).HasMaxLength(512);
            e.Property(r => r.IsBuiltIn).HasDefaultValue(false);
            e.Property(r => r.IsSiteLevel).HasDefaultValue(false);
        });

        modelBuilder.Entity<PolarUserTenantMembership>(e =>
        {
            e.ToTable("polar_user_tenant_memberships");
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.UserId, m.TenantId, m.RoleId }).IsUnique();
            e.HasIndex(m => new { m.TenantId, m.IsActive });
            e.Property(m => m.JoinedAt).IsRequired();
            e.HasOne(m => m.Role!).WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformAuditLogEntry>(e =>
        {
            e.ToTable("polar_platform_audit_log");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.OccurredAt);
            e.HasIndex(p => new { p.ActorUserId, p.OccurredAt });
            e.HasIndex(p => new { p.TargetTenantId, p.OccurredAt });
            e.Property(p => p.ActorEmail).IsRequired().HasMaxLength(320);
            e.Property(p => p.EntityType).IsRequired().HasMaxLength(128);
            e.Property(p => p.Action).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.JustificationKind).HasConversion<string>().HasMaxLength(32);
            e.Property(p => p.JustificationText).HasMaxLength(2048);
        });

        return modelBuilder;
    }
}
