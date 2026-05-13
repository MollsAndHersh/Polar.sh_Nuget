namespace PolarSharp.EcommerceStoreManagement.Services;

/// <summary>
/// Admin-facing refund service — wraps Polar's <c>POST /v1/refunds/</c> with strongly-typed
/// reason codes, support for full and partial refunds, and audit-log integration. Refunds
/// themselves are NOT persisted locally (Polar is the source of truth for financial records);
/// the listing method fetches directly from Polar.
/// </summary>
public interface IRefundService
{
    /// <summary>Refunds the entire order at the supplied reason code.</summary>
    /// <param name="polarOrderId">The Polar order id to refund.</param>
    /// <param name="reason">Why the refund is being issued.</param>
    /// <param name="comment">Optional free-form note — required when <paramref name="reason"/> is <see cref="RefundReason.Other"/>.</param>
    /// <param name="ct">Cancellation.</param>
    Task<Result<RefundResult, RefundError>> IssueFullRefundAsync(
        string polarOrderId,
        RefundReason reason,
        string? comment,
        CancellationToken ct = default);

    /// <summary>Refunds a partial amount of the order.</summary>
    /// <param name="polarOrderId">The Polar order id.</param>
    /// <param name="amount">The amount to refund, in minor currency units.</param>
    /// <param name="currency">ISO 4217 currency code — must match the order's currency.</param>
    /// <param name="reason">Why the refund is being issued.</param>
    /// <param name="comment">Optional free-form note — required when <paramref name="reason"/> is <see cref="RefundReason.Other"/>.</param>
    /// <param name="ct">Cancellation.</param>
    Task<Result<RefundResult, RefundError>> IssuePartialRefundAsync(
        string polarOrderId,
        int amount,
        string currency,
        RefundReason reason,
        string? comment,
        CancellationToken ct = default);

    /// <summary>Returns every refund Polar has recorded for the supplied order.</summary>
    Task<Result<IReadOnlyList<RefundRecord>, RefundError>> ListForOrderAsync(
        string polarOrderId,
        CancellationToken ct = default);
}

/// <summary>The outcome of a successful refund issuance.</summary>
public sealed record RefundResult(string RefundId, int AmountRefunded, string Currency, DateTimeOffset CreatedAt);

/// <summary>A historical refund record — one row per refund Polar has issued against the order.</summary>
public sealed record RefundRecord(string RefundId, int Amount, string Currency, RefundReason Reason, string? Comment, DateTimeOffset CreatedAt);

/// <summary>Typed error for refund flows.</summary>
public sealed record RefundError(RefundErrorKind Kind, string Message);

/// <summary>Recoverable refund failure modes.</summary>
public enum RefundErrorKind
{
    /// <summary>The order id is unknown to Polar.</summary>
    OrderNotFound,
    /// <summary>The order has already been fully refunded.</summary>
    AlreadyFullyRefunded,
    /// <summary>The requested partial amount exceeds the order's remaining refundable balance.</summary>
    AmountExceedsRefundable,
    /// <summary>The supplied currency doesn't match the order's currency.</summary>
    CurrencyMismatch,
    /// <summary>Reason is <see cref="RefundReason.Other"/> but no comment was supplied.</summary>
    CommentRequired,
    /// <summary>Generic Polar API failure (5xx, timeout, etc.).</summary>
    PolarApiFailure,
}
