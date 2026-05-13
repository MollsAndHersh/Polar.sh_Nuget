using Microsoft.AspNetCore.Identity;
using PolarSharp.MultiTenant.Identity.Authorization;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// The ONLY API path for granting or revoking <see cref="PolarApplicationUser.IsAppMasterAdmin"/>.
/// </summary>
/// <remarks>
/// <para>
/// All operations are gated by <see cref="RequireAppMasterAdminAttribute"/> at both the
/// concrete implementation and at the policy layer (defense in depth: the call is rejected
/// even if the attribute is somehow bypassed at the route layer).
/// </para>
/// <para>
/// This interface deliberately exposes NO method that creates the FIRST AppMasterAdmin —
/// that's the bootstrap path's job (<c>AppMasterAdminBootstrapper</c>), which runs ONCE at
/// startup and is unreachable from any HTTP-driven code path. Self-elevation is impossible:
/// every call to grant the flag requires an existing AppMasterAdmin.
/// </para>
/// </remarks>
public interface IAppMasterAdminProvisioning
{
    /// <summary>Grants <see cref="PolarApplicationUser.IsAppMasterAdmin"/> to the named user and adds them to the <see cref="PolarRoles.AppMasterAdmin"/> role.</summary>
    /// <param name="userId">The user to elevate.</param>
    /// <param name="ct">Cancellation.</param>
    Task<IdentityResult> GrantAppMasterAdminAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Revokes <see cref="PolarApplicationUser.IsAppMasterAdmin"/> from the named user and removes them from the <see cref="PolarRoles.AppMasterAdmin"/> role.</summary>
    /// <param name="userId">The user to demote.</param>
    /// <param name="ct">Cancellation.</param>
    /// <remarks>
    /// Refuses to demote the LAST remaining AppMasterAdmin — there must always be at least one
    /// active platform admin so the next bootstrap doesn't have to run. Returns a failed
    /// <see cref="IdentityResult"/> with a descriptive error in that case.
    /// </remarks>
    Task<IdentityResult> RevokeAppMasterAdminAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns the currently active AppMasterAdmin users.</summary>
    Task<IReadOnlyList<PolarApplicationUser>> ListAppMasterAdminsAsync(CancellationToken ct = default);
}
