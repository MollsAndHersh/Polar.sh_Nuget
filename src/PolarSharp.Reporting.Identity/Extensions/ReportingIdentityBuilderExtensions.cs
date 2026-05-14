using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.Reporting.Identity.Extensions;

/// <summary>
/// Registration helpers for the V20-005 Phase 3 reporting-identity bridge.
/// </summary>
public static class ReportingIdentityBuilderExtensions
{
    /// <summary>
    /// Replaces the default <see cref="SignInManager{PolarApplicationUser}"/> with
    /// <see cref="PolarSnapshotSignInManager"/>, so user sign-in / sign-out drives the
    /// snapshot orchestrator. Call AFTER <c>AddPolarIdentity()</c> + <c>AddPolarReportingSnapshot()</c>.
    /// </summary>
    /// <remarks>
    /// Idempotent — calling twice replaces the registration with the same decorator.
    /// </remarks>
    public static IServiceCollection AddPolarReportingIdentityHook(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Replace (not TryAdd) — we WANT to override the SignInManager registered by AddIdentity().
        services.AddScoped<SignInManager<PolarApplicationUser>, PolarSnapshotSignInManager>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="PolarSnapshotHeartbeatMiddleware"/> in the request pipeline.
    /// Place AFTER <c>UseAuthentication()</c> and BEFORE <c>UseAuthorization()</c> so the
    /// middleware sees the hydrated <c>HttpContext.User</c>.
    /// </summary>
    public static IApplicationBuilder UsePolarReportingIdentityHeartbeat(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<PolarSnapshotHeartbeatMiddleware>();
    }
}
