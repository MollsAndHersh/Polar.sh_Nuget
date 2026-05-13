using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// M:N join row — one user's role assignment within ONE tenant.
/// </summary>
/// <remarks>
/// <para>
/// A single user (<see cref="PolarApplicationUser"/>, identified globally by email) can hold
/// memberships in multiple tenants with a different role per tenant. Active memberships are
/// detected via <see cref="IsActive"/> (combined with <see cref="RevokedAt"/> for the audit
/// trail of who-was-removed-when).
/// </para>
/// <para>
/// <strong>Tenant ownership:</strong> implements <see cref="ITenantOwned"/> so the standard
/// PolarSharp tenant-isolation query filter applies — a user from tenant A's API call cannot
/// enumerate memberships in tenant B (defense in depth: also enforced by the cross-tenant
/// access flag in <c>TenantAwareDbContextBase</c> and SQL RLS at the database layer).
/// </para>
/// <para>
/// <strong>TenantAdmin invariant:</strong> the <c>TenantAdminInvariantValidator</c>
/// <see cref="Microsoft.Extensions.Hosting.IHostedService"/> verifies on every startup that
/// every tenant has at least one <see cref="IsActive"/> = <see langword="true"/> membership
/// where the role is <see cref="PolarRoles.TenantAdmin"/>. AppMasterAdmins are NOT counted —
/// each tenant must have its own dedicated administrator.
/// </para>
/// </remarks>
public class PolarUserTenantMembership : ITenantOwned, IFakeDataAware
{
    /// <summary>Surrogate primary key for the membership row.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to <see cref="PolarApplicationUser"/>'s identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>FK to the tenant — stored as <see cref="Guid"/> for clean Identity-side joins.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Explicit interface implementation — projects <see cref="TenantId"/> to the string contract used by <c>TenantAwareDbContextBase</c>.</summary>
    string ITenantOwned.TenantId => TenantId.ToString();

    /// <summary>FK to <see cref="PolarApplicationRole"/>'s identifier — defines what permissions this membership grants.</summary>
    public Guid RoleId { get; set; }

    /// <summary>UTC timestamp when the membership was first created.</summary>
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>UTC timestamp when the membership was revoked. <see langword="null"/> for active memberships.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Active flag — set to <see langword="false"/> when membership is revoked. Soft-delete preserves audit history.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc/>
    /// <remarks>Set to <see langword="true"/> when the membership was created by the data-seeding package; filtered by the dual query filter when the tenant has <c>AllowFakeData</c> = <see langword="false"/>.</remarks>
    public bool IsFakeData { get; set; }

    /// <summary>Navigation to the related user. Lazy-loaded only when explicitly Included.</summary>
    public PolarApplicationUser? User { get; set; }

    /// <summary>Navigation to the related role. Lazy-loaded only when explicitly Included.</summary>
    public PolarApplicationRole? Role { get; set; }
}
