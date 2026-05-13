using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Per-request mutable signal that the request has been authorized for AppMasterAdmin
/// cross-tenant access. Set by <c>[AllowCrossTenant]</c>'s endpoint filter AFTER dual-flag
/// verification (DB <see cref="PolarApplicationUser.IsAppMasterAdmin"/> +
/// <see cref="PolarRoles.AppMasterAdmin"/> role claim).
/// </summary>
/// <remarks>
/// Registered as scoped DI; reads via the same-scope <see cref="IAppMasterAdminCrossTenantContext"/>
/// resolution from Phase 2's EF Core base. Splitting the read interface (Phase 2) from the
/// write interface (this one) keeps the EF Core base layer free of any Identity dependency.
/// </remarks>
public interface IAppMasterAdminCrossTenantSignal : IAppMasterAdminCrossTenantContext
{
    /// <summary>Marks the current request as authorized for cross-tenant access.</summary>
    /// <remarks>
    /// Idempotent — calling more than once is harmless. Once set within a request scope, the
    /// flag CANNOT be cleared (defense against late-stage code paths that might unintentionally
    /// disable cross-tenant access while a query is in flight).
    /// </remarks>
    void GrantCrossTenantAccess();
}

/// <summary>Default scoped implementation — a single boolean held for the request lifetime.</summary>
internal sealed class AppMasterAdminCrossTenantSignal : IAppMasterAdminCrossTenantSignal
{
    private bool _granted;

    public bool IsAllowedCrossTenantAccess => _granted;

    public void GrantCrossTenantAccess() => _granted = true;
}
