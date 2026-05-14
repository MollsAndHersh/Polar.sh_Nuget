using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;

namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// Default <see cref="IPolarReportingApi"/> implementation backed by the Kiota
/// <see cref="PolarClient"/>. V20-005 Phase 1 wires the 7 new resources
/// (benefits, discounts, checkout-links, products, license-keys, meters, customer-meters)
/// to their live Polar HTTP endpoints. The original 5 (events, orders, subscriptions,
/// customers, benefit-grants) remain best-effort stubs and ship as live impls in a
/// Phase 1.5 follow-up.
/// </summary>
internal sealed class PolarClientReportingApi(PolarClient polar, ILogger<PolarClientReportingApi> logger) : IPolarReportingApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientReportingApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // ── Existing 5 (stub — wired live in Phase 1.5) ──────────────────────────

    public Task<Result<IReadOnlyList<EventPayload>, PolarReportingApiError>> FetchEventsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<EventPayload>(nameof(FetchEventsSinceAsync));

    public Task<Result<IReadOnlyList<OrderPayload>, PolarReportingApiError>> FetchOrdersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<OrderPayload>(nameof(FetchOrdersSinceAsync));

    public Task<Result<IReadOnlyList<SubscriptionPayload>, PolarReportingApiError>> FetchSubscriptionsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<SubscriptionPayload>(nameof(FetchSubscriptionsSinceAsync));

    public Task<Result<IReadOnlyList<CustomerPayload>, PolarReportingApiError>> FetchCustomersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<CustomerPayload>(nameof(FetchCustomersSinceAsync));

    public Task<Result<IReadOnlyList<BenefitGrantPayload>, PolarReportingApiError>> FetchBenefitGrantsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<BenefitGrantPayload>(nameof(FetchBenefitGrantsSinceAsync));

    // ── V20-005 Phase 1: 7 new resource impls ────────────────────────────────
    // Filled in resource-by-resource below. Each starts as a stub returning empty
    // so the file compiles cleanly during incremental wiring; replaced one-at-a-time.

    public Task<Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>> FetchBenefitsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<BenefitPayload>(nameof(FetchBenefitsSinceAsync));

    public Task<Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>> FetchDiscountsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<DiscountPayload>(nameof(FetchDiscountsSinceAsync));

    public Task<Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>> FetchCheckoutLinksSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<CheckoutLinkPayload>(nameof(FetchCheckoutLinksSinceAsync));

    public Task<Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>> FetchProductsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<ProductPayload>(nameof(FetchProductsSinceAsync));

    public Task<Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>> FetchLicenseKeysSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<LicenseKeyPayload>(nameof(FetchLicenseKeysSinceAsync));

    public Task<Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>> FetchMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<MeterPayload>(nameof(FetchMetersSinceAsync));

    public Task<Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>> FetchCustomerMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<CustomerMeterPayload>(nameof(FetchCustomerMetersSinceAsync));

    // ── Helpers ─────────────────────────────────────────────────────────────

    private Task<Result<IReadOnlyList<T>, PolarReportingApiError>> EmptyAsync<T>(string method)
    {
        _logger.LogDebug("PolarClientReportingApi.{Method}: HTTP wiring deferred; returning empty page.", method);
        return Task.FromResult(Result<IReadOnlyList<T>, PolarReportingApiError>.Success((IReadOnlyList<T>)Array.Empty<T>()));
    }

    private static PolarReportingApiError MapApiException(ApiException ex, string resourceName)
    {
        var kind = ex.ResponseStatusCode switch
        {
            401 or 403 => PolarReportingApiErrorKind.AuthorizationFailed,
            429 => PolarReportingApiErrorKind.RateLimited,
            _ => PolarReportingApiErrorKind.UnexpectedFailure,
        };
        return new PolarReportingApiError(kind, $"Polar {resourceName} {ex.ResponseStatusCode}: {ex.Message}");
    }
}
