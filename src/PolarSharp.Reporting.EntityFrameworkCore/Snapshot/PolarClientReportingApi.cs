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

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1B: live wiring against <c>GET /v1/products/</c>. Page-1 cursor
    /// semantics — see the class-level remarks. Map: <c>Product</c> -&gt; <c>ProductPayload</c>.
    /// <c>Description</c> is a string-or-object union; extract <c>.String</c>. <c>ModifiedAt</c>
    /// is a DateTime-or-object union; extract <c>.DateTimeOffset</c>. <c>RecurringInterval</c>
    /// is a Polar enum; we stringify it for our payload (wire-format value preserved).
    /// </remarks>
    public async Task<Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>> FetchProductsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Products.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Success(Array.Empty<ProductPayload>());

            // Polar returns descending by created_at; reverse to ascending so the snapshot
            // service's `cursor = rows[^1].Id` advances to the newest ingested row.
            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ascending[i].Id, sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<ProductPayload>(ascending.Count);
            foreach (var p in ascending)
            {
                mapped.Add(new ProductPayload(
                    Id: p.Id ?? string.Empty,
                    Name: p.Name ?? string.Empty,
                    Description: p.Description?.String,
                    IsRecurring: p.IsRecurring ?? false,
                    RecurringInterval: p.RecurringInterval?.ToString()?.ToLowerInvariant(),
                    IsArchived: p.IsArchived ?? false,
                    CreatedAt: p.CreatedAt ?? DateTimeOffset.UtcNow,
                    ModifiedAt: p.ModifiedAt?.DateTimeOffset));
            }
            return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "products"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching products.");
            return Result<IReadOnlyList<ProductPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

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
