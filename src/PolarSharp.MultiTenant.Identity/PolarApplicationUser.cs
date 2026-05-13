using Microsoft.AspNetCore.Identity;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// PolarSharp's <see cref="IdentityUser{Guid}"/> extension — the canonical user account record.
/// </summary>
/// <remarks>
/// <para>
/// All IDs in PolarSharp Identity are <see cref="Guid"/> for SaaS interoperability. A user is a
/// SINGLE identity (unique <see cref="IdentityUser{TKey}.NormalizedEmail"/> globally) that can be
/// granted access to MULTIPLE tenants via <see cref="Memberships"/> (the M:N join). This shape
/// supports both single-tenant SaaS (one membership per user) and multi-tenant SaaS (one user
/// can be admin in tenant A and read-only in tenant B).
/// </para>
/// <para>
/// <strong>Site-level vs tenant-level distinction:</strong>
/// <see cref="IsAppMasterAdmin"/> is the SaaS-provider staff flag. A user with this flag set
/// AND the <see cref="PolarRoles.AppMasterAdmin"/> role claim can access cross-tenant routes
/// (only those annotated with <c>[AllowCrossTenant]</c>). The flag itself is NOT settable via
/// any tenant-scoped API — only via <see cref="IAppMasterAdminProvisioning"/>, which itself
/// requires existing AppMasterAdmin authorization (preventing self-elevation by tenant admins).
/// </para>
/// </remarks>
public class PolarApplicationUser : IdentityUser<Guid>
{
    /// <summary>The user's display name. Optional — falls back to <see cref="IdentityUser{TKey}.UserName"/>.</summary>
    public string? FullName { get; set; }

    /// <summary>UTC timestamp when this user was first created in PolarSharp.</summary>
    public DateTimeOffset OnboardedAt { get; set; }

    /// <summary>UTC timestamp of the user's most recent successful sign-in. <see langword="null"/> until first sign-in.</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// SITE-LEVEL flag — when true, the user is SaaS-provider staff with potential cross-tenant
    /// access (gated by the <c>[AllowCrossTenant]</c> attribute and dual role-claim verification).
    /// </summary>
    /// <remarks>
    /// This flag is NOT settable via any tenant-scoped Identity API. It can only be set via
    /// <see cref="IAppMasterAdminProvisioning"/>, which requires the calling user to already be
    /// an AppMasterAdmin. The bootstrap path (creating the very first AppMasterAdmin) runs once
    /// at startup via <c>AppMasterAdminBootstrapper</c>.
    /// </remarks>
    public bool IsAppMasterAdmin { get; set; }

    /// <summary>The user's active and historical tenant memberships. Each row represents access to one tenant with one role.</summary>
    public ICollection<PolarUserTenantMembership> Memberships { get; set; } = [];
}
