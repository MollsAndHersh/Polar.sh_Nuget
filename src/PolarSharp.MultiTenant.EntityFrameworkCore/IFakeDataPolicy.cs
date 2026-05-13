namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Ambient context that exposes the current tenant's <c>AllowFakeData</c> flag to
/// <see cref="TenantAwareDbContextBase"/>'s global query filter.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by <c>PolarSharp.EcommerceStoreManagement</c>'s business-profile service, which
/// reads the flag from <c>TenantBusinessProfile.AllowFakeData</c> and caches it via
/// <see cref="IPolarTenantCache"/>. If <c>EcommerceStoreManagement</c> is not installed, the
/// policy defaults to <see langword="false"/> (fake data excluded — the safe default).
/// </para>
/// <para>
/// Resolved once per DbContext at construction time. Tenants that flip the toggle mid-request
/// will see the change on the next DbContext resolution, not the current one — this is
/// intentional: half-applied filters within a single transaction would be confusing.
/// </para>
/// </remarks>
public interface IFakeDataPolicy
{
    /// <summary>Gets a value indicating whether the current tenant has fake-data inclusion enabled.</summary>
    bool AllowFakeData { get; }
}
