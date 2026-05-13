using Microsoft.Extensions.Logging;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Publishing;

/// <summary>
/// Default <see cref="IPolarPublishingApi"/> implementation backed by the Kiota
/// <see cref="PolarClient"/>. Best-effort wiring — TASK-V20-001 covers sandbox validation.
/// Until sandbox-validated, every method returns <c>UnexpectedFailure</c> so production
/// consumers fail loudly rather than silently no-oping.
/// </summary>
internal sealed class PolarClientPublishingApi(PolarClient polar, ILogger<PolarClientPublishingApi> logger) : IPolarPublishingApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientPublishingApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<Result<string, PolarPublishApiError>> CreateProductAsync(PolarProductPayload payload, CancellationToken ct) =>
        StubAsync(nameof(CreateProductAsync), payload.Name);

    public Task<Result<string, PolarPublishApiError>> UpdateProductAsync(string polarId, PolarProductPayload payload, CancellationToken ct) =>
        StubAsync(nameof(UpdateProductAsync), polarId);

    public Task<Result<string, PolarPublishApiError>> CreateBenefitAsync(PolarBenefitPayload payload, CancellationToken ct) =>
        StubAsync(nameof(CreateBenefitAsync), payload.Name);

    public Task<Result<string, PolarPublishApiError>> UpdateBenefitAsync(string polarId, PolarBenefitPayload payload, CancellationToken ct) =>
        StubAsync(nameof(UpdateBenefitAsync), polarId);

    public Task<Result<string, PolarPublishApiError>> CreateDiscountAsync(PolarDiscountPayload payload, CancellationToken ct) =>
        StubAsync(nameof(CreateDiscountAsync), payload.Name);

    public Task<Result<string, PolarPublishApiError>> UpdateDiscountAsync(string polarId, PolarDiscountPayload payload, CancellationToken ct) =>
        StubAsync(nameof(UpdateDiscountAsync), polarId);

    public Task<Result<string, PolarPublishApiError>> CreateCheckoutLinkAsync(PolarCheckoutLinkPayload payload, CancellationToken ct) =>
        StubAsync(nameof(CreateCheckoutLinkAsync), payload.Name);

    private Task<Result<string, PolarPublishApiError>> StubAsync(string method, string identifier)
    {
        _logger.LogWarning(
            "PolarClientPublishingApi.{Method} called for '{Identifier}' but HTTP wiring is deferred to TASK-V20-001 — returning UnexpectedFailure.",
            method, identifier);
        return Task.FromResult(Result<string, PolarPublishApiError>.Failure(new PolarPublishApiError(
            PolarPublishApiErrorKind.UnexpectedFailure,
            $"Polar publish HTTP wiring is deferred to TASK-V20-001 ({method}) — supply a custom IPolarPublishingApi implementation or wait for the sandbox-validated wrapper.")));
    }
}
