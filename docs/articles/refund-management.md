# Refund management

`IRefundService` in `PolarSharp.EcommerceStoreManagement` is the host-facing admin surface for issuing refunds against Polar orders. Wraps Polar's `POST /v1/refunds/` with structured reason codes, full + partial support, and automatic audit-log capture.

## Service surface

```csharp
public interface IRefundService
{
    Task<Result<RefundResult, PolarError>> IssueFullRefundAsync(
        string polarOrderId,
        RefundReason reason,
        Option<string> comment,
        CancellationToken ct = default);

    Task<Result<RefundResult, PolarError>> IssuePartialRefundAsync(
        string polarOrderId,
        decimal amount,
        string currency,
        RefundReason reason,
        Option<string> comment,
        CancellationToken ct = default);
}
```

`RefundReason` covers Polar's documented values: `CustomerRequest`, `DuplicateCharge`, `Fraudulent`, `ProductNotReceived`, `ProductUnacceptable`, `Other`.

## Audit-log capture

Every successful refund call writes an `AdminAuditLogEntry` row via the active `IAuditLogActorProvider` — `ActorEmail`, `EntityType="PolarOrder"`, `EntityId=polarOrderId`, `Action=Update`, `BeforeValues`/`AfterValues` reflecting the amount-refunded transition. Visible to tenant operators via the reporting `GetAuditTrailAsync` query (`[RequirePolarPermission(ViewAuditLog)]`).

## Storage posture

Refunds are NOT stored locally. Polar is the source of truth for financial records. The audit log captures the *intent* and the *actor* of every refund; the canonical refund record lives in Polar and is mirrored into the reporting snapshot table `ReportOrderRefundEntity` on the next snapshot tick.

## Permissions

Refund endpoints gate behind `[RequirePolarPermission(IssueRefund)]`. The `TenantAdmin` and `TenantUser` built-in roles hold this permission; `ReadOnly` and `Auditor` do not.

## v2.0 deferral

The concrete Polar HTTP impl behind `IPolarRefundsApi` is a deferred stub (`PolarClientRefundsApi`, tracked as TASK-V20-002) until the Kiota request builder for `/v1/refunds/` is wired through `PolarClient`. Until then `IssueFullRefundAsync` / `IssuePartialRefundAsync` log a warning and return `UnexpectedFailure`. Hosts implementing custom `IPolarRefundsApi` against the live Polar API work today.

## Strict framing

PolarSharp does NOT talk to Stripe. Ever. Anywhere. Refunds initiated through this service hit Polar's API; Polar's backend orchestrates the Stripe refund out-of-band. The host never sees a Stripe credential through PolarSharp.
