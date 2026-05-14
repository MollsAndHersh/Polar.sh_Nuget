namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// Polar HTTP boundary for the snapshot service — paginated reads of every Polar resource
/// the snapshot ingests, since the per-resource checkpoint.
/// </summary>
/// <remarks>
/// V20-005 (this task) wires every method to live Polar HTTP via the Kiota-generated
/// <c>PolarClient</c>. Each <c>Fetch*SinceAsync</c> is a best-effort cursor through
/// <c>/v1/{resource}/?page=…</c> using the per-resource checkpoint as the resume point.
/// Empty pages signal end-of-stream; the snapshot service uses that as its "no more rows
/// right now" indicator and advances the checkpoint to the last seen id.
/// </remarks>
internal interface IPolarReportingApi
{
    Task<Result<IReadOnlyList<EventPayload>, PolarReportingApiError>> FetchEventsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>> FetchOrdersSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>> FetchSubscriptionsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>> FetchCustomersSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>> FetchBenefitGrantsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);

    // ── V20-005 Phase 1: 7 new resource fetchers ──────────────────────────────────

    Task<Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>> FetchBenefitsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>> FetchDiscountsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>> FetchCheckoutLinksSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>> FetchProductsSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>> FetchLicenseKeysSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>> FetchMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>> FetchCustomerMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct);
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

// ── V20-005 Phase 1: payload records for the 7 new resources ──────────────────────

internal sealed record BenefitPayload(string Id, string Name, string Kind, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? ModifiedAt);

internal sealed record DiscountPayload(string Id, string Name, string? Code, string Type, long? AmountOff, decimal? PercentOff, string? Currency, int? RedemptionsSoFar, int? MaxRedemptions, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset CreatedAt);

internal sealed record CheckoutLinkPayload(string Id, string Label, IReadOnlyList<string> ProductIds, string? Url, string? SuccessUrl, bool AllowDiscountCodes, DateTimeOffset CreatedAt);

internal sealed record ProductPayload(string Id, string Name, string? Description, bool IsRecurring, string? RecurringInterval, bool IsArchived, DateTimeOffset CreatedAt, DateTimeOffset? ModifiedAt);

internal sealed record LicenseKeyPayload(string Id, string CustomerId, string? BenefitId, string? DisplayKey, string Status, int? LimitActivations, int? ActivationsUsed, DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt);

internal sealed record MeterPayload(string Id, string Name, string AggregationKind, DateTimeOffset CreatedAt);

internal sealed record CustomerMeterPayload(string Id, string CustomerId, string MeterId, decimal ConsumedUnits, decimal? CreditedUnits, DateTimeOffset CreatedAt, DateTimeOffset? ModifiedAt);

internal sealed record PolarReportingApiError(PolarReportingApiErrorKind Kind, string Message);

internal enum PolarReportingApiErrorKind
{
    AuthorizationFailed,
    RateLimited,
    UnexpectedFailure,
}
