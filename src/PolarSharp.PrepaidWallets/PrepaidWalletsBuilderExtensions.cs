using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets;

/// <summary>
/// Registration extensions for the PolarSharp PrepaidWallets feature.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 02 "Event-Sourced Wallet with Comprehensive Economic Modeling",
/// the wallet ships as an event-sourced aggregate + MediatR + CQRS + projections +
/// snapshot strategy. The core package contains the domain logic; storage backends
/// (Marten or EF Core) plug in via separate provider packages.
/// </para>
/// <para>
/// <strong>Phase 20 ships the registration scaffold</strong>; the full Wallet aggregate,
/// MediatR command/query handlers, projections, behaviors (idempotency / validation /
/// logging / transaction), and snapshot strategy land in Phase 20.x. The shape of
/// <c>AddPolarPrepaidWallets()</c> won't change; subsequent phases just register
/// concrete services through it.
/// </para>
/// </remarks>
public static class PrepaidWalletsBuilderExtensions
{
    /// <summary>Registers the PolarSharp PrepaidWallets feature.</summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPolarPrepaidWallets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Phase 20.x: register MediatR + behaviors + Wallet aggregate factory + projections.
        return services;
    }
}
