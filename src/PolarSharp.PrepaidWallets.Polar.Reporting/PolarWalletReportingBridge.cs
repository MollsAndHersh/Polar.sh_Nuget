using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.Polar.Reporting;

/// <summary>
/// Polar bridge surfacing wallet sections inside the existing PolarSharp.Reporting client.
/// Also wires the audit-log integration so every wallet operation writes a corresponding
/// AdminAuditLogEntry alongside the wallet event.
/// </summary>
/// <remarks>
/// Phase 22 ships the bridge package shell; full reporting integration + audit log
/// SaveChangesInterceptor lands in Phase 22.x. Reports added (per amendment 1):
/// CustomerPurchaseHistoryReport, PurchaseOrderProgressReport, RefundReconciliationReport.
/// </remarks>
public static class PolarWalletReportingBridgeExtensions
{
    /// <summary>Registers the Polar reporting bridge for the wallet.</summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPolarWalletReportingBridge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
