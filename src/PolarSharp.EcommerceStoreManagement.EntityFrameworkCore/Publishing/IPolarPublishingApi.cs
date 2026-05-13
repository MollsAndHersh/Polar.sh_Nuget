namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Publishing;

/// <summary>
/// Polar HTTP boundary for the publishing flow. One method per (entity-type, verb) combo.
/// Wraps the Kiota-generated <see cref="PolarClient"/> calls.
/// </summary>
/// <remarks>
/// Real implementation (<c>PolarClientPublishingApi</c>) is best-effort and not yet
/// sandbox-validated — see TASK-V20-001 in <c>~/TASKS.md</c>.
/// </remarks>
internal interface IPolarPublishingApi
{
    Task<Result<string, PolarPublishApiError>> CreateProductAsync(PolarProductPayload payload, CancellationToken ct);
    Task<Result<string, PolarPublishApiError>> UpdateProductAsync(string polarId, PolarProductPayload payload, CancellationToken ct);
    Task<Result<string, PolarPublishApiError>> CreateBenefitAsync(PolarBenefitPayload payload, CancellationToken ct);
    Task<Result<string, PolarPublishApiError>> UpdateBenefitAsync(string polarId, PolarBenefitPayload payload, CancellationToken ct);
    Task<Result<string, PolarPublishApiError>> CreateDiscountAsync(PolarDiscountPayload payload, CancellationToken ct);
    Task<Result<string, PolarPublishApiError>> UpdateDiscountAsync(string polarId, PolarDiscountPayload payload, CancellationToken ct);
    Task<Result<string, PolarPublishApiError>> CreateCheckoutLinkAsync(PolarCheckoutLinkPayload payload, CancellationToken ct);
}

internal sealed record PolarProductPayload(
    string Name,
    string? Description,
    bool IsRecurring,
    int? PriceAmount,
    string? PriceCurrency,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<string> AttachedPolarBenefitIds);

internal sealed record PolarBenefitPayload(
    string Kind,
    string Name,
    string Description,
    string PropertiesJson);

internal sealed record PolarDiscountPayload(
    string Name,
    string Type,
    int? AmountOff,
    decimal? PercentageOff,
    string? Currency,
    string? Code,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int? MaxRedemptions);

internal sealed record PolarCheckoutLinkPayload(
    string Name,
    IReadOnlyList<string> PolarProductIds,
    string? SuccessUrl,
    string? CancelUrl,
    bool AllowDiscountCodes,
    bool RequireBillingAddress);

internal sealed record PolarPublishApiError(PolarPublishApiErrorKind Kind, string Message);

internal enum PolarPublishApiErrorKind
{
    ValidationFailed,
    NotFound,
    UnexpectedFailure,
}
