using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.Polar.Checkout;

/// <summary>
/// Polar bridge wiring the wallet into the catalog checkout flow with three configurable
/// modes (wallet-only / hybrid / Polar-only per tenant).
/// </summary>
/// <remarks>
/// Phase 22 ships the bridge package shell; full impl lands in Phase 22.x:
/// <list type="bullet">
///   <item>PolarWalletCheckoutInterceptor with the 3 checkout modes</item>
///   <item>PolarWalletRefundConverter for refund-as-credit (saves Polar refund fee)</item>
///   <item>PolarWalletSubscriptionDebitor for recurring billing-cycle ticks</item>
///   <item>Order/PO/product linkage snapshotted at debit time (per amendment 1)</item>
/// </list>
/// </remarks>
public static class PolarWalletCheckoutBridgeExtensions
{
    /// <summary>Registers the Polar checkout bridge for the wallet.</summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPolarWalletCheckoutBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
