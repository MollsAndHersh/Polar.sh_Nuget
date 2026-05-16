using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Polar.Identity;

/// <summary>
/// Polar bridge wiring PolarSharp.MultiTenant.Identity's <c>ICurrentUser</c> into
/// the wallet's <see cref="IWalletIdentityProvider"/> abstraction. This is the
/// multi-tenant integration path; single-tenant hosts use
/// <c>PolarSharp.PrepaidWallets.AspNetCore.Identity</c> instead (per Case Study 05).
/// </summary>
/// <remarks>
/// Phase 22 ships the bridge package shell; full impl wiring ICurrentUser →
/// IWalletIdentityProvider + CustomerTransactionContext adapter for IP/UA capture
/// lands in Phase 22.x.
/// </remarks>
public static class PolarWalletIdentityBridgeExtensions
{
    /// <summary>Registers the Polar multi-tenant identity bridge for the wallet.</summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPolarWalletIdentityBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
