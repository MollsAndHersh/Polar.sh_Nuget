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

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1E: live wiring against <c>GET /v1/benefits/</c>. <c>Benefit</c> is a
    /// discriminated union wrapping seven subtypes (BenefitCustom, BenefitDiscord,
    /// BenefitDownloadables, BenefitFeatureFlag, BenefitGitHubRepository, BenefitLicenseKeys,
    /// BenefitMeterCredit) — exactly one subtype property is non-null per row. We probe each
    /// in order and extract the shared fields (Id, Description, IsActive, CreatedAt). The
    /// discriminator becomes the <c>Kind</c> value, mapped to Polar's wire-format string.
    /// </remarks>
    public async Task<Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>> FetchBenefitsSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Benefits.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Success(Array.Empty<BenefitPayload>());

            var ascending = items.AsEnumerable().Reverse().ToList();
            if (!string.IsNullOrEmpty(sinceId))
            {
                var idx = -1;
                for (var i = 0; i < ascending.Count; i++)
                {
                    if (string.Equals(ExtractBenefitId(ascending[i]), sinceId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i; break;
                    }
                }
                if (idx >= 0) ascending = [.. ascending.Skip(idx + 1)];
            }

            var mapped = new List<BenefitPayload>(ascending.Count);
            foreach (var b in ascending)
            {
                var payload = MapBenefit(b);
                if (payload is not null) mapped.Add(payload);
            }
            return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "benefits"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching benefits.");
            return Result<IReadOnlyList<BenefitPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Extracts the Polar id from whichever subtype variant is populated on the
    /// <see cref="global::PolarSharp.Generated.Models.Benefit"/> wrapper.
    /// </summary>
    private static string? ExtractBenefitId(global::PolarSharp.Generated.Models.Benefit b) =>
        b.BenefitCustom?.Id
        ?? b.BenefitDiscord?.Id
        ?? b.BenefitDownloadables?.Id
        ?? b.BenefitFeatureFlag?.Id
        ?? b.BenefitGitHubRepository?.Id
        ?? b.BenefitLicenseKeys?.Id
        ?? b.BenefitMeterCredit?.Id;

    /// <summary>
    /// Probes each subtype on the Benefit discriminator wrapper, returning a populated
    /// payload for the first non-null variant. Returns null if the wrapper has no populated
    /// variant (Polar returned a row with an unknown <c>type</c> discriminator — we log
    /// + skip rather than fail).
    /// </summary>
    private BenefitPayload? MapBenefit(global::PolarSharp.Generated.Models.Benefit b)
    {
        // Each subtype has the same shared shape (Id, Description, Selectable, CreatedAt,
        // ModifiedAt). The `Name` we surface is the Description truncated to fit the
        // entity's 256-char limit; Polar's Benefit model has no separate display-name
        // field for most subtypes.
        if (b.BenefitCustom is { } c)              return Build(c.Id, c.Description, "custom", c.Selectable, c.CreatedAt, c.ModifiedAt?.DateTimeOffset);
        if (b.BenefitDiscord is { } d)             return Build(d.Id, d.Description, "discord", d.Selectable, d.CreatedAt, d.ModifiedAt?.DateTimeOffset);
        if (b.BenefitDownloadables is { } dl)      return Build(dl.Id, dl.Description, "downloadables", dl.Selectable, dl.CreatedAt, dl.ModifiedAt?.DateTimeOffset);
        if (b.BenefitFeatureFlag is { } ff)        return Build(ff.Id, ff.Description, "feature_flag", ff.Selectable, ff.CreatedAt, ff.ModifiedAt?.DateTimeOffset);
        if (b.BenefitGitHubRepository is { } gh)   return Build(gh.Id, gh.Description, "github_repository", gh.Selectable, gh.CreatedAt, gh.ModifiedAt?.DateTimeOffset);
        if (b.BenefitLicenseKeys is { } lk)        return Build(lk.Id, lk.Description, "license_keys", lk.Selectable, lk.CreatedAt, lk.ModifiedAt?.DateTimeOffset);
        if (b.BenefitMeterCredit is { } mc)        return Build(mc.Id, mc.Description, "meter_credit", mc.Selectable, mc.CreatedAt, mc.ModifiedAt?.DateTimeOffset);
        _logger.LogWarning("Benefit row had no populated subtype variant — skipping in snapshot ingestion.");
        return null;

        static BenefitPayload Build(string? id, string? description, string kind, bool? selectable, DateTimeOffset? createdAt, DateTimeOffset? modifiedAt) =>
            new(
                Id: id ?? string.Empty,
                Name: Truncate(description ?? kind, 256),
                Kind: kind,
                Description: description,
                IsActive: selectable ?? true,
                CreatedAt: createdAt ?? DateTimeOffset.UtcNow,
                ModifiedAt: modifiedAt);

        static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }

    public Task<Result<IReadOnlyList<DiscountPayload>, PolarReportingApiError>> FetchDiscountsSinceAsync(string? sinceId, int pageSize, CancellationToken ct) =>
        EmptyAsync<DiscountPayload>(nameof(FetchDiscountsSinceAsync));

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1G: live wiring against <c>GET /v1/checkout-links/</c>. Polar's
    /// CheckoutLink has multiple string-or-object union fields (<c>Label</c>,
    /// <c>SuccessUrl</c>, <c>ModifiedAt</c>, <c>DiscountId</c>, <c>ReturnUrl</c>); each
    /// extracts its <c>.String</c> / <c>.DateTimeOffset</c> variant. The top-level
    /// <c>Url</c> is a plain <c>string?</c> with private setter — exposed via Polar's
    /// JSON response. <c>Products</c> is a list of <c>CheckoutLinkProduct</c>; we collect
    /// each product's Id into a CSV string for the snapshot row (entity has a 2048-char
    /// ProductIdsCsv field). The sensitive <c>ClientSecret</c> field is deliberately NOT
    /// mapped.
    /// </remarks>
    public async Task<Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>> FetchCheckoutLinksSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.CheckoutLinks.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Success(Array.Empty<CheckoutLinkPayload>());

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

            var mapped = new List<CheckoutLinkPayload>(ascending.Count);
            foreach (var cl in ascending)
            {
                var productIds = cl.Products?
                    .Where(p => !string.IsNullOrEmpty(p.Id))
                    .Select(p => p.Id!)
                    .ToList() ?? [];
                mapped.Add(new CheckoutLinkPayload(
                    Id: cl.Id ?? string.Empty,
                    Label: cl.Label?.String ?? "(unnamed checkout link)",
                    ProductIds: productIds,
                    Url: cl.Url,
                    SuccessUrl: cl.SuccessUrl?.String,
                    AllowDiscountCodes: cl.AllowDiscountCodes ?? true,
                    CreatedAt: cl.CreatedAt ?? DateTimeOffset.UtcNow));
            }
            return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "checkout-links"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching checkout links.");
            return Result<IReadOnlyList<CheckoutLinkPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

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

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1D: live wiring against <c>GET /v1/license-keys/</c>. Multiple
    /// union-wrapped fields: <c>ExpiresAt</c> + <c>ModifiedAt</c> + <c>LastValidatedAt</c>
    /// (DateTimeOffset-or-object); <c>LimitActivations</c> + <c>LimitUsage</c>
    /// (Integer-or-object). Extract the populated variant on each. The raw <c>Key</c> is
    /// deliberately NOT mapped — we surface <c>DisplayKey</c> (Polar's masked-for-UI form)
    /// so the snapshot never carries the sensitive raw key string. <c>Usage</c> is mapped
    /// to <c>ActivationsUsed</c>; semantically it's "validation count" in Polar's model
    /// (distinct from raw activation count), but it's the only usage-counter Polar
    /// surfaces at the list level — close-enough for the snapshot.
    /// </remarks>
    public async Task<Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>> FetchLicenseKeysSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.LicenseKeys.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Success(Array.Empty<LicenseKeyPayload>());

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

            var mapped = new List<LicenseKeyPayload>(ascending.Count);
            foreach (var lk in ascending)
            {
                mapped.Add(new LicenseKeyPayload(
                    Id: lk.Id ?? string.Empty,
                    CustomerId: lk.CustomerId ?? string.Empty,
                    BenefitId: lk.BenefitId,
                    DisplayKey: lk.DisplayKey,
                    Status: lk.Status?.ToString()?.ToLowerInvariant() ?? "granted",
                    LimitActivations: lk.LimitActivations?.Integer,
                    ActivationsUsed: lk.Usage,
                    ExpiresAt: lk.ExpiresAt?.DateTimeOffset,
                    CreatedAt: lk.CreatedAt ?? DateTimeOffset.UtcNow));
            }
            return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "license-keys"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching license keys.");
            return Result<IReadOnlyList<LicenseKeyPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1F: live wiring against <c>GET /v1/meters/</c>. Polar's
    /// <c>Meter.Aggregation</c> is a discriminated union on a field (not the top-level
    /// shape) — variants: <c>CountAggregation</c>, <c>PropertyAggregation</c>,
    /// <c>UniqueAggregation</c>. Each carries a <c>Func</c> field naming the actual
    /// aggregation function (sum, max, avg, count, unique). We probe each variant in
    /// order and surface the function name as the snapshot's <c>AggregationKind</c>.
    /// </remarks>
    public async Task<Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>> FetchMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.Meters.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Success(Array.Empty<MeterPayload>());

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

            var mapped = new List<MeterPayload>(ascending.Count);
            foreach (var m in ascending)
            {
                mapped.Add(new MeterPayload(
                    Id: m.Id ?? string.Empty,
                    Name: m.Name ?? "(unnamed)",
                    AggregationKind: ExtractAggregationKind(m.Aggregation),
                    CreatedAt: m.CreatedAt ?? DateTimeOffset.UtcNow));
            }
            return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "meters"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching meters.");
            return Result<IReadOnlyList<MeterPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Probes the Meter_aggregation discriminator wrapper and returns the aggregation
    /// function name (sum / count / avg / max / min / unique) as a lowercase wire string.
    /// Defaults to "unknown" when no variant is populated.
    /// </summary>
    private static string ExtractAggregationKind(global::PolarSharp.Generated.Models.Meter.Meter_aggregation? agg)
    {
        if (agg is null) return "unknown";
        if (agg.CountAggregation is { Func: { Length: > 0 } cf }) return cf.ToLowerInvariant();
        if (agg.PropertyAggregation is { Func: { } pf }) return pf.ToString().ToLowerInvariant();
        if (agg.UniqueAggregation is { Func: { Length: > 0 } uf }) return uf.ToLowerInvariant();
        return "unknown";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// V20-005 Phase 1C: live wiring against <c>GET /v1/customer-meters/</c>. Mostly flat
    /// fields; only <c>ModifiedAt</c> is a string-or-DateTimeOffset union (extract
    /// <c>.DateTimeOffset</c>). Polar's <c>ConsumedUnits</c> is <c>double?</c> wire-side; we
    /// surface <c>decimal</c> in <c>CustomerMeterPayload</c> for consistent reporting math.
    /// </remarks>
    public async Task<Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>> FetchCustomerMetersSinceAsync(string? sinceId, int pageSize, CancellationToken ct)
    {
        try
        {
            var response = await _polar.CustomerMeters.EmptyPathSegment.GetAsync(cfg =>
            {
                cfg.QueryParameters.Limit = pageSize;
                cfg.QueryParameters.Page = 1;
            }, ct).ConfigureAwait(false);

            var items = response?.Items ?? [];
            if (items.Count == 0) return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Success(Array.Empty<CustomerMeterPayload>());

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

            var mapped = new List<CustomerMeterPayload>(ascending.Count);
            foreach (var cm in ascending)
            {
                mapped.Add(new CustomerMeterPayload(
                    Id: cm.Id ?? string.Empty,
                    CustomerId: cm.CustomerId ?? string.Empty,
                    MeterId: cm.MeterId ?? string.Empty,
                    ConsumedUnits: (decimal)(cm.ConsumedUnits ?? 0d),
                    CreditedUnits: (decimal?)cm.CreditedUnits,
                    CreatedAt: cm.CreatedAt ?? DateTimeOffset.UtcNow,
                    ModifiedAt: cm.ModifiedAt?.DateTimeOffset));
            }
            return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Success(mapped);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Failure(MapApiException(ex, "customer-meters"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching customer meters.");
            return Result<IReadOnlyList<CustomerMeterPayload>, PolarReportingApiError>.Failure(new PolarReportingApiError(
                PolarReportingApiErrorKind.UnexpectedFailure, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

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
