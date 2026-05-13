namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Minimal Polar HTTP boundary used by <see cref="LicenseKeyValidator"/>. Wraps the
/// Kiota-generated <c>PolarClient.V1.LicenseKeys.Validate.PostAsync</c> call behind a typed
/// interface so the validator can be unit-tested with a fake API.
/// </summary>
/// <remarks>
/// The real implementation (<c>PolarClientLicenseKeysApi</c>) is best-effort and not yet
/// sandbox-validated — see TASK-V20-003 in <c>~/TASKS.md</c>.
/// </remarks>
internal interface IPolarLicenseKeysApi
{
    /// <summary>POSTs a license-key validation request to Polar; returns the validated response or a typed error.</summary>
    Task<Result<LicenseKeyApiResponse, LicenseKeyApiError>> ValidateAsync(LicenseKeyApiRequest request, CancellationToken ct);
}

/// <summary>Input to the Polar license-key validation endpoint.</summary>
internal sealed record LicenseKeyApiRequest(string Key, string OrganizationId);

/// <summary>Polar's validated-license-key response, projected to the fields PolarSharp surfaces.</summary>
internal sealed record LicenseKeyApiResponse(
    string LicenseKeyId,
    string? CustomerId,
    DateTimeOffset? ExpiresAt,
    int? ActivationsRemaining,
    bool IsActiveAtPolar);

/// <summary>Polar-side license-key error discriminator.</summary>
internal sealed record LicenseKeyApiError(LicenseKeyApiErrorKind Kind, string Message);

/// <summary>Polar-side license-key failure modes.</summary>
internal enum LicenseKeyApiErrorKind
{
    MalformedKey,
    NotFound,
    UnexpectedFailure,
}
