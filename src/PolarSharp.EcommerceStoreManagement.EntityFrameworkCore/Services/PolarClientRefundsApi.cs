using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStoreManagement.Services;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IPolarRefundsApi"/> implementation that calls Polar's HTTP API via the
/// Kiota-generated <see cref="PolarClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Best-effort HTTP wiring — TASK-V20-002.</strong> This wrapper is shipped in
/// v1.3.0 to give consumers a default implementation, but the exact request/response field
/// mapping has not been validated against a live Polar sandbox. The shape is based on the
/// Kiota-generated <c>RefundCreate</c> / <c>Refund</c> models. A follow-up task
/// (TASK-V20-002 in <c>~/TASKS.md</c>) covers sandbox validation, edge-case error mapping,
/// and pagination tuning for the listing endpoint.
/// </para>
/// <para>
/// Tests for <see cref="RefundService"/> use a fake <see cref="IPolarRefundsApi"/> so the
/// service-level logic is fully covered regardless of this wrapper's sandbox-validation
/// status.
/// </para>
/// </remarks>
internal sealed class PolarClientRefundsApi(PolarClient polar, ILogger<PolarClientRefundsApi> logger) : IPolarRefundsApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientRefundsApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<Result<RefundApiResponse, RefundApiError>> CreateRefundAsync(RefundApiRequest request, CancellationToken ct)
    {
        // TODO TASK-V20-002: wire to _polar.V1.Refunds.EmptyPathSegment.PostAsync(RefundCreate, ct)
        // and map RefundCreate <- RefundApiRequest, RefundApiResponse <- Refund. Until sandbox-
        // validated, return UnexpectedFailure so consumers fail loudly rather than silently no-op.
        _logger.LogWarning(
            "PolarClientRefundsApi.CreateRefundAsync called for order {PolarOrderId} but HTTP wiring is deferred to TASK-V20-002 — returning UnexpectedFailure.",
            request.PolarOrderId);
        return Task.FromResult(Result<RefundApiResponse, RefundApiError>.Failure(new RefundApiError(
            RefundApiErrorKind.UnexpectedFailure,
            "Polar refund HTTP wiring is deferred to TASK-V20-002 — supply a custom IPolarRefundsApi implementation or wait for the sandbox-validated wrapper.")));
    }

    public Task<Result<IReadOnlyList<RefundApiResponse>, RefundApiError>> ListRefundsForOrderAsync(string polarOrderId, CancellationToken ct)
    {
        // TODO TASK-V20-002: wire to _polar.V1.Refunds.EmptyPathSegment.GetAsync(...) with
        // order-id query parameter, paginate, project to RefundApiResponse.
        _logger.LogWarning(
            "PolarClientRefundsApi.ListRefundsForOrderAsync called for order {PolarOrderId} but HTTP wiring is deferred to TASK-V20-002 — returning UnexpectedFailure.",
            polarOrderId);
        return Task.FromResult(Result<IReadOnlyList<RefundApiResponse>, RefundApiError>.Failure(new RefundApiError(
            RefundApiErrorKind.UnexpectedFailure,
            "Polar refund-listing HTTP wiring is deferred to TASK-V20-002.")));
    }
}
