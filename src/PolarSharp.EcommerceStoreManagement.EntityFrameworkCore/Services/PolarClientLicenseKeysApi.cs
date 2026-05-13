using Microsoft.Extensions.Logging;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IPolarLicenseKeysApi"/> implementation that calls Polar's HTTP API via
/// the Kiota-generated <see cref="PolarClient"/>.
/// </summary>
/// <remarks>
/// Best-effort HTTP wiring — TASK-V20-003. See <see cref="PolarClientRefundsApi"/> for the
/// same posture; the field mapping from <c>ValidatedLicenseKey</c> to
/// <see cref="LicenseKeyApiResponse"/> is shipped but not sandbox-validated.
/// </remarks>
internal sealed class PolarClientLicenseKeysApi(PolarClient polar, ILogger<PolarClientLicenseKeysApi> logger) : IPolarLicenseKeysApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientLicenseKeysApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<Result<LicenseKeyApiResponse, LicenseKeyApiError>> ValidateAsync(LicenseKeyApiRequest request, CancellationToken ct)
    {
        // TODO TASK-V20-003: wire to _polar.V1.LicenseKeys.Validate.PostAsync(LicenseKeyValidate { Key, OrganizationId }, ct)
        // and map ValidatedLicenseKey -> LicenseKeyApiResponse. Until sandbox-validated, return
        // UnexpectedFailure so consumers fail loudly.
        _logger.LogWarning(
            "PolarClientLicenseKeysApi.ValidateAsync called for org {OrganizationId} but HTTP wiring is deferred to TASK-V20-003 — returning UnexpectedFailure.",
            request.OrganizationId);
        return Task.FromResult(Result<LicenseKeyApiResponse, LicenseKeyApiError>.Failure(new LicenseKeyApiError(
            LicenseKeyApiErrorKind.UnexpectedFailure,
            "Polar license-key HTTP wiring is deferred to TASK-V20-003 — supply a custom IPolarLicenseKeysApi implementation or wait for the sandbox-validated wrapper.")));
    }
}
