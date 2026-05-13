using PolarSharp.EcommerceStoreManagement.Services;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Minimal Polar HTTP boundary used by <see cref="RefundService"/>. Wraps the Kiota-generated
/// <c>PolarClient</c> calls behind a typed interface so the service can be unit-tested with
/// a fake API.
/// </summary>
/// <remarks>
/// The real implementation (<c>PolarClientRefundsApi</c>) is best-effort and not yet
/// sandbox-validated — see TASK-V20-002 in <c>~/TASKS.md</c>.
/// </remarks>
internal interface IPolarRefundsApi
{
    /// <summary>POSTs a refund creation request to Polar; returns the created refund or a typed error.</summary>
    Task<Result<RefundApiResponse, RefundApiError>> CreateRefundAsync(RefundApiRequest request, CancellationToken ct);

    /// <summary>GETs all refunds associated with the supplied order.</summary>
    Task<Result<IReadOnlyList<RefundApiResponse>, RefundApiError>> ListRefundsForOrderAsync(string polarOrderId, CancellationToken ct);
}

/// <summary>Input to the Polar refund-create endpoint. <see cref="Amount"/> is null for full refunds.</summary>
internal sealed record RefundApiRequest(
    string PolarOrderId,
    int? Amount,
    string? Currency,
    RefundReason Reason,
    string? Comment);

/// <summary>Polar's refund response, projected to the fields PolarSharp surfaces.</summary>
internal sealed record RefundApiResponse(
    string RefundId,
    int Amount,
    string Currency,
    RefundReason Reason,
    string? Comment,
    DateTimeOffset CreatedAt);

/// <summary>Polar-side refund error discriminator. Distinct from the public <see cref="RefundErrorKind"/> so the service can add its own validation cases on top.</summary>
internal sealed record RefundApiError(RefundApiErrorKind Kind, string Message);

/// <summary>Polar-side refund failure modes.</summary>
internal enum RefundApiErrorKind
{
    OrderNotFound,
    AlreadyFullyRefunded,
    AmountExceedsRefundable,
    CurrencyMismatch,
    UnexpectedFailure,
}
