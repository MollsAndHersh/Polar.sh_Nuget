using Microsoft.AspNetCore.Authorization;

namespace PolarSharp.MultiTenant.Identity.Authorization;

/// <summary>
/// Policy name constants and helpers for registering PolarSharp authorization policies.
/// </summary>
/// <remarks>
/// Policies are auto-registered by <c>AddPolarIdentity()</c>: one
/// <see cref="AppMasterAdmin"/> policy plus one per-permission policy
/// (<see cref="PermissionPrefix"/> + permission name). Hosts can also register their own
/// policies using these handlers.
/// </remarks>
public static class PolarAuthorizationPolicies
{
    /// <summary>Policy name for the site-level AppMasterAdmin gate.</summary>
    public const string AppMasterAdmin = "PolarSharp.AppMasterAdmin";

    /// <summary>Prefix for per-permission policy names. Full name: <c>PolarSharp.Permission.{permission}</c>.</summary>
    public const string PermissionPrefix = "PolarSharp.Permission.";

    /// <summary>Registers all built-in PolarSharp authorization policies on the supplied <see cref="AuthorizationOptions"/>.</summary>
    /// <param name="options">The authorization options being configured.</param>
    public static void RegisterAllBuiltIn(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(AppMasterAdmin, p => p
            .RequireAuthenticatedUser()
            .AddRequirements(new AppMasterAdminRequirement()));

        foreach (var perm in Enum.GetValues<PolarPermission>())
        {
            options.AddPolicy($"{PermissionPrefix}{perm}", p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new PolarPermissionRequirement(perm)));
        }
    }
}

/// <summary>Requirement: the user passes the AppMasterAdmin dual-flag check.</summary>
public sealed class AppMasterAdminRequirement : IAuthorizationRequirement { }

/// <summary>Requirement: the user holds a specific <see cref="PolarPermission"/> in their current tenant context.</summary>
public sealed class PolarPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>The required permission.</summary>
    public PolarPermission Permission { get; }

    /// <summary>Initializes the requirement with the named permission.</summary>
    public PolarPermissionRequirement(PolarPermission permission) => Permission = permission;
}
