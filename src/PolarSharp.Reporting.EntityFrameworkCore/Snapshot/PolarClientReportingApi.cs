using Microsoft.Extensions.Logging;

namespace PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

/// <summary>
/// Default <see cref="IPolarReportingApi"/> implementation backed by the Kiota
/// <see cref="PolarClient"/>. Best-effort wiring — TASK-V20-005 covers sandbox validation.
/// Until validated, every method returns an empty page so snapshot runs are honest no-ops
/// rather than throwing.
/// </summary>
/// <remarks>
/// An empty-page return is intentionally distinct from a failure return: it means "the
/// snapshot has nothing more to pull right now" rather than "the request errored." The
/// snapshot service walks until it sees an empty page, treats that as end-of-stream,
/// advances the checkpoint to the last seen id, and moves on. With this stub in place every
/// resource's snapshot is a no-op (the checkpoint isn't advanced because no rows were seen),
/// so re-running is idempotent.
/// </remarks>
internal sealed class PolarClientReportingApi(PolarClient polar, ILogger<PolarClientReportingApi> logger) : IPolarReportingApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientReportingApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

    private Task<Result<IReadOnlyList<T>, PolarReportingApiError>> EmptyAsync<T>(string method)
    {
        _logger.LogDebug("PolarClientReportingApi.{Method}: HTTP wiring deferred to TASK-V20-005; returning empty page.", method);
        return Task.FromResult(Result<IReadOnlyList<T>, PolarReportingApiError>.Success((IReadOnlyList<T>)Array.Empty<T>()));
    }
}
