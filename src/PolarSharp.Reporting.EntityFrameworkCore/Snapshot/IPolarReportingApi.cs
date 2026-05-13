namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// Polar HTTP boundary for the snapshot service — paginated reads of Polar's events,
/// orders, subscriptions, customers, and benefit grants since the per-resource checkpoint.
/// </summary>
/// <remarks>
/// Real implementation (<c>PolarClientReportingApi</c>) is best-effort and not yet
/// sandbox-validated — see TASK-V20-005 in <c>~/TASKS.md</c>. Until validated, the default
/// implementation returns empty pages so consumers fail gracefully rather than throwing.
/// </remarks>
internal interface IPolarReportingApi
{
    Task<Result<IReadOnlyList<EventPayload>, PolarReportingApiError>> FetchEventsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>> FetchOrdersSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>> FetchSubscriptionsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>> FetchCustomersSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>> FetchBenefitGrantsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
}

/// <summary>Polar's event log row.</summary>
internal sealed record EventPayload(string Id, string Type, DateTimeOffset OccurredAt, string? PayloadJson);

/// <summary>Polar's order shape (with line items + refunds nested).</summary>
internal sealed record OrderPayload(
    string Id,
    string Number,
    string CustomerId,
    string Status,
    long Amount,
    long TaxAmount,
    long RefundedAmount,
    string Currency,
    string? InvoiceUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FulfilledAt,
    IReadOnlyList<OrderLineItemPayload> LineItems,
    IReadOnlyList<OrderRefundPayload> Refunds);

internal sealed record OrderLineItemPayload(string ProductId, string ProductName, string? PriceId, int Quantity, long UnitAmount, long LineTotal, long DiscountAmount, long TaxAmount);

internal sealed record OrderRefundPayload(string Id, long Amount, string Currency, string Reason, DateTimeOffset CreatedAt);

internal sealed record SubscriptionPayload(string Id, string CustomerId, string ProductId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CanceledAt);

internal sealed record CustomerPayload(string Id, string Email, string? Name, string Currency, DateTimeOffset CreatedAt);

internal sealed record BenefitGrantPayload(string Id, string CustomerId, string? OrderId, string BenefitId, string BenefitName, string BenefitKind, bool IsGranted, DateTimeOffset? GrantedAt, DateTimeOffset? RevokedAt);

internal sealed record PolarReportingApiError(PolarReportingApiErrorKind Kind, string Message);

internal enum PolarReportingApiErrorKind
{
    AuthorizationFailed,
    RateLimited,
    UnexpectedFailure,
}
