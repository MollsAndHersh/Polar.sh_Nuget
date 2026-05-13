using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.Generated.Models;
using KiotaRefundReason = PolarSharp.Generated.Models.RefundReason;
using OurRefundReason = PolarSharp.EcommerceStoreManagement.RefundReason;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IPolarRefundsApi"/> implementation that calls Polar's HTTP API via the
/// Kiota-generated <see cref="PolarClient"/> — wired against the live sandbox path
/// <c>POST /v1/refunds/</c> and <c>GET /v1/refunds/?order_id=…</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Implementation status (TASK-V20-002).</strong> Wired and verified against
/// <c>https://sandbox-api.polar.sh</c> using the loaded <c>POLAR_SANDBOX_TOKEN</c>. The
/// happy-path mapping (request → <see cref="RefundCreate"/>; response → <see cref="Refund"/>)
/// is exercised end-to-end. Error mapping uses Polar's HTTP status to discriminate the
/// common cases (404 = OrderNotFound, 400 = AmountExceedsRefundable / AlreadyFullyRefunded /
/// CurrencyMismatch via response body, 5xx = UnexpectedFailure). When the body doesn't
/// match a known shape, the wrapper falls back to UnexpectedFailure so callers always see
/// a typed error rather than an exception.
/// </para>
/// <para>
/// Tests for <see cref="RefundService"/> use a fake <see cref="IPolarRefundsApi"/> so the
/// service-level logic is fully covered without hitting the network. This wrapper itself is
/// validated by an opt-in integration test (gated by the <c>POLAR_SANDBOX_TOKEN</c> env var)
/// in <c>PolarSharp.IntegrationTests</c>.
/// </para>
/// </remarks>
internal sealed class PolarClientRefundsApi(PolarClient polar, ILogger<PolarClientRefundsApi> logger) : IPolarRefundsApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientRefundsApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<Result<RefundApiResponse, RefundApiError>> CreateRefundAsync(RefundApiRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new RefundCreate
        {
            OrderId = request.PolarOrderId,
            Amount = request.Amount,                                          // null = full refund per Polar spec
            Reason = MapReasonToPolar(request.Reason),
            Comment = string.IsNullOrEmpty(request.Comment)
                ? null
                : new RefundCreate.RefundCreate_comment { String = request.Comment },
        };

        try
        {
            var refund = await _polar.Refunds.EmptyPathSegment.PostAsync(body, cancellationToken: ct).ConfigureAwait(false);
            if (refund is null)
            {
                _logger.LogWarning("Polar refund POST returned null body for order {PolarOrderId}.", request.PolarOrderId);
                return Result<RefundApiResponse, RefundApiError>.Failure(new RefundApiError(
                    RefundApiErrorKind.UnexpectedFailure,
                    "Polar returned an empty refund response."));
            }

            return Result<RefundApiResponse, RefundApiError>.Success(MapResponseFromPolar(refund, request.Comment));
        }
        catch (ApiException ex)
        {
            return Result<RefundApiResponse, RefundApiError>.Failure(MapApiException(ex, request.PolarOrderId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error POSTing refund for order {PolarOrderId}.", request.PolarOrderId);
            return Result<RefundApiResponse, RefundApiError>.Failure(new RefundApiError(
                RefundApiErrorKind.UnexpectedFailure,
                $"Unexpected error: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<RefundApiResponse>, RefundApiError>> ListRefundsForOrderAsync(string polarOrderId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(polarOrderId);

        var aggregated = new List<RefundApiResponse>();
        var page = 1;
        const int hardPageCap = 50;            // safety bound — refunds-per-order is small in practice
        try
        {
            while (page <= hardPageCap)
            {
                var pageNum = page;            // capture for the closure
                var result = await _polar.Refunds.EmptyPathSegment.GetAsync(
                    cfg =>
                    {
                        cfg.QueryParameters.OrderId = polarOrderId;
                        cfg.QueryParameters.Page = pageNum;
                        cfg.QueryParameters.Limit = 100;
                    },
                    cancellationToken: ct).ConfigureAwait(false);

                if (result?.Items is null || result.Items.Count == 0) break;

                foreach (var refund in result.Items)
                {
                    aggregated.Add(MapResponseFromPolar(refund, comment: null));
                }

                if (result.Pagination is null) break;
                if (result.Pagination.MaxPage is not { } maxPage || page >= maxPage) break;
                page++;
            }

            return Result<IReadOnlyList<RefundApiResponse>, RefundApiError>.Success(aggregated);
        }
        catch (ApiException ex)
        {
            return Result<IReadOnlyList<RefundApiResponse>, RefundApiError>.Failure(MapApiException(ex, polarOrderId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing refunds for order {PolarOrderId}.", polarOrderId);
            return Result<IReadOnlyList<RefundApiResponse>, RefundApiError>.Failure(new RefundApiError(
                RefundApiErrorKind.UnexpectedFailure,
                $"Unexpected error: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static RefundApiResponse MapResponseFromPolar(Refund refund, string? comment) =>
        new(
            RefundId: refund.Id ?? string.Empty,
            Amount: refund.Amount ?? 0,
            Currency: refund.Currency ?? string.Empty,
            Reason: MapReasonFromPolar(refund.Reason),
            Comment: comment,                                                 // Polar doesn't echo comment in the response shape
            CreatedAt: refund.CreatedAt ?? DateTimeOffset.UtcNow);

    // RefundReason mapping note (TASK-V20-002 follow-up):
    // Polar's wire enum has 7 values; our public RefundReason has 6 with different semantics.
    // - Direct matches: CustomerRequest <-> Customer_request, DuplicateCharge <-> Duplicate,
    //   Fraudulent <-> Fraudulent, Other <-> Other.
    // - One-way collapse on send: ProductNotReceived / ProductUnacceptable have no Polar
    //   wire equivalent, so we send `Other` and the original intent should be expressed in
    //   the comment (RefundService validation enforces a comment when reason is Other).
    // - Receive-side collapse: Polar's Service_disruption, Satisfaction_guarantee, and
    //   Dispute_prevention values map to our Other since our enum doesn't enumerate them.
    // Tracked for v2.0: expand our public RefundReason to be a true 1:1 mirror of Polar's
    // wire enum (additive, will require a Public API snapshot bump).
    private static KiotaRefundReason MapReasonToPolar(OurRefundReason ours) => ours switch
    {
        OurRefundReason.CustomerRequest => KiotaRefundReason.Customer_request,
        OurRefundReason.DuplicateCharge => KiotaRefundReason.Duplicate,
        OurRefundReason.Fraudulent => KiotaRefundReason.Fraudulent,
        OurRefundReason.ProductNotReceived => KiotaRefundReason.Other,
        OurRefundReason.ProductUnacceptable => KiotaRefundReason.Other,
        OurRefundReason.Other => KiotaRefundReason.Other,
        _ => KiotaRefundReason.Other,
    };

    private static OurRefundReason MapReasonFromPolar(KiotaRefundReason? polar) => polar switch
    {
        KiotaRefundReason.Customer_request => OurRefundReason.CustomerRequest,
        KiotaRefundReason.Duplicate => OurRefundReason.DuplicateCharge,
        KiotaRefundReason.Fraudulent => OurRefundReason.Fraudulent,
        KiotaRefundReason.Other => OurRefundReason.Other,
        // Lossy collapse — see mapping note above.
        KiotaRefundReason.Service_disruption => OurRefundReason.Other,
        KiotaRefundReason.Satisfaction_guarantee => OurRefundReason.Other,
        KiotaRefundReason.Dispute_prevention => OurRefundReason.Other,
        _ => OurRefundReason.Other,
    };

    private static RefundApiError MapApiException(ApiException ex, string polarOrderId)
    {
        var kind = ex.ResponseStatusCode switch
        {
            404 => RefundApiErrorKind.OrderNotFound,
            400 when ex.Message.Contains("already refunded", StringComparison.OrdinalIgnoreCase)
                  || ex.Message.Contains("already_refunded", StringComparison.OrdinalIgnoreCase)
                => RefundApiErrorKind.AlreadyFullyRefunded,
            400 when ex.Message.Contains("exceed", StringComparison.OrdinalIgnoreCase)
                  || ex.Message.Contains("amount", StringComparison.OrdinalIgnoreCase)
                => RefundApiErrorKind.AmountExceedsRefundable,
            400 when ex.Message.Contains("currency", StringComparison.OrdinalIgnoreCase)
                => RefundApiErrorKind.CurrencyMismatch,
            _ => RefundApiErrorKind.UnexpectedFailure,
        };
        return new RefundApiError(kind, $"Polar refund {ex.ResponseStatusCode}: {ex.Message}");
    }
}
