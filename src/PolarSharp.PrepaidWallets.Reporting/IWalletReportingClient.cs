namespace PolarSharp.PrepaidWallets.Reporting;

/// <summary>
/// Read-side reporting API for the prepaid wallet (lift-safe; no PolarSharp.* coupling).
/// Hosts query wallet balances, history, funding sources, and audience-scoped aggregates
/// through this interface; the implementation reads from wallet projections.
/// </summary>
/// <remarks>
/// Phase 22 ships the interface shell; concrete projection-backed implementation lands in
/// Phase 22.x with the CustomerPurchaseHistoryReport, PurchaseOrderProgressReport, and
/// RefundReconciliationReport shapes per amendment 1.
/// </remarks>
public interface IWalletReportingClient
{
    /// <summary>Returns the current wallet balance for a customer.</summary>
    Task<long> GetBalanceAsync(Guid customerId, CancellationToken ct = default);
}
