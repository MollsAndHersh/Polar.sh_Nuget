using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// EF Core DbContext for PolarSharp Identity — extends the standard ASP.NET Core
/// <see cref="IdentityDbContext{TUser, TRole, TKey}"/> with PolarSharp's M:N
/// <see cref="PolarUserTenantMembership"/> table and the site-level
/// <see cref="PlatformAuditLogEntry"/> table.
/// </summary>
/// <remarks>
/// <para>
/// This context does NOT inherit from <c>TenantAwareDbContextBase</c>. The reason: Identity
/// tables (Users, Roles, Logins, Tokens) are intentionally NOT tenant-scoped — a user is a
/// global identity that holds memberships across N tenants. Membership rows themselves ARE
/// tenant-owned (and the global query filter is applied below in <see cref="OnModelCreating"/>).
/// </para>
/// <para>
/// <strong>Tenant scope at the membership layer:</strong> the global query filter on
/// <see cref="PolarUserTenantMembership"/> matches the standard PolarSharp pattern — current
/// tenant ID stamping at insert, current-tenant filter on read, AppMasterAdmin bypass when
/// <c>[AllowCrossTenant]</c> is opted in. This means a tenant admin in tenant A cannot
/// enumerate tenant B's user list (they can't see B's memberships at all).
/// </para>
/// <para>
/// Provider-specific subclasses (<c>SqlServerPolarUserDbContext</c>, etc.) supply the
/// concrete connection string and migrations.
/// </para>
/// </remarks>
public class PolarUserDbContext : IdentityDbContext<PolarApplicationUser, PolarApplicationRole, Guid>
{
    private readonly Guid? _currentTenantId;
    private readonly bool _isAppMasterAdminCrossTenant;

    /// <summary>Initializes a new <see cref="PolarUserDbContext"/> with the supplied options.</summary>
    /// <param name="options">EF Core options including provider and connection string.</param>
    public PolarUserDbContext(DbContextOptions<PolarUserDbContext> options) : base(options) { }

    /// <summary>
    /// Initializes the DbContext with optional tenant + cross-tenant context. When the tenant
    /// context is omitted (e.g., during AppMasterAdmin user-management or the bootstrap flow),
    /// the membership query filter is bypassed.
    /// </summary>
    protected PolarUserDbContext(
        DbContextOptions options,
        Guid? currentTenantId,
        bool isAppMasterAdminCrossTenant)
        : base(options)
    {
        _currentTenantId = currentTenantId;
        _isAppMasterAdminCrossTenant = isAppMasterAdminCrossTenant;
    }

    /// <summary>The M:N user-tenant membership table.</summary>
    public DbSet<PolarUserTenantMembership> Memberships => Set<PolarUserTenantMembership>();

    /// <summary>The site-level cross-tenant audit log.</summary>
    public DbSet<PlatformAuditLogEntry> PlatformAuditLog => Set<PlatformAuditLogEntry>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // Shared schema config — same code that hosts call from their own DbContext when they
        // chose the "Use host DbContext" deployment shape (PolarIdentityOptions.SqlOptions).
        modelBuilder.AddPolarIdentitySchema();

        // Tenant scope filter on memberships. The host-DbContext deployment shape provides
        // this filter via its own override (the host's DbContext typically inherits
        // TenantAwareDbContextBase or applies the filter inline).
        modelBuilder.Entity<PolarUserTenantMembership>().HasQueryFilter(m =>
            _isAppMasterAdminCrossTenant ||
            _currentTenantId == null ||
            m.TenantId == _currentTenantId.Value);
    }
}
