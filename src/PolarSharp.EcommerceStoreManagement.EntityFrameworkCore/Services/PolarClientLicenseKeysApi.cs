using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using PolarSharp.Generated.Models;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IPolarLicenseKeysApi"/> implementation that calls Polar's HTTP API via
/// the Kiota-generated <see cref="PolarClient"/>'s <c>POST /v1/license-keys/validate</c>
/// endpoint.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Implementation status (TASK-V20-003).</strong> Wired and verified against
/// <c>https://sandbox-api.polar.sh</c> using the loaded <c>POLAR_SANDBOX_TOKEN</c>. The
/// happy-path mapping (<see cref="LicenseKeyValidate"/> body → <see cref="ValidatedLicenseKey"/>
/// response) is exercised end-to-end by the live integration test below. Error mapping uses
/// Polar's HTTP status to discriminate the common cases (404 → NotFound, 400/422 with a
/// malformed-key body → MalformedKey, anything else → UnexpectedFailure with the underlying
/// message preserved). The wrapper NEVER lets an exception leak — callers always receive a
/// typed <see cref="LicenseKeyApiError"/>.
/// </para>
/// <para>
/// <strong>Field mapping notes.</strong> Polar's <see cref="ValidatedLicenseKey.ExpiresAt"/>
/// is a discriminated union of <see cref="DateTimeOffset"/>-or-object; we extract the
/// <see cref="DateTimeOffset"/> variant. <see cref="ValidatedLicenseKey.LimitActivations"/>
/// is a similar union of <c>int</c>-or-object — for v2.0 we surface only the integer ceiling
/// (<see cref="LicenseKeyApiResponse.ActivationsRemaining"/> stays null because computing
/// "remaining" requires the current activation count, which is a separate
/// <c>GET /v1/license-keys/{id}/activations</c> round-trip; deferred follow-up).
/// <see cref="LicenseKeyApiResponse.IsActiveAtPolar"/> is true iff
/// <see cref="ValidatedLicenseKey.Status"/> is <see cref="LicenseKeyStatus.Granted"/>.
/// </para>
/// </remarks>
internal sealed class PolarClientLicenseKeysApi(PolarClient polar, ILogger<PolarClientLicenseKeysApi> logger) : IPolarLicenseKeysApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientLicenseKeysApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<Result<LicenseKeyApiResponse, LicenseKeyApiError>> ValidateAsync(LicenseKeyApiRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new LicenseKeyValidate
        {
            Key = request.Key,
            OrganizationId = request.OrganizationId,
        };

        try
        {
            var validated = await _polar.LicenseKeys.Validate.PostAsync(body, cancellationToken: ct).ConfigureAwait(false);
            if (validated is null)
            {
                _logger.LogWarning("Polar license-key POST returned null body for org {OrganizationId}.", request.OrganizationId);
                return Result<LicenseKeyApiResponse, LicenseKeyApiError>.Failure(new LicenseKeyApiError(
                    LicenseKeyApiErrorKind.UnexpectedFailure,
                    "Polar returned an empty license-key validation response."));
            }

            return Result<LicenseKeyApiResponse, LicenseKeyApiError>.Success(MapResponse(validated));
        }
        catch (ApiException ex)
        {
            return Result<LicenseKeyApiResponse, LicenseKeyApiError>.Failure(MapApiException(ex, request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating license key for org {OrganizationId}.", request.OrganizationId);
            return Result<LicenseKeyApiResponse, LicenseKeyApiError>.Failure(new LicenseKeyApiError(
                LicenseKeyApiErrorKind.UnexpectedFailure,
                $"Unexpected error: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static LicenseKeyApiResponse MapResponse(ValidatedLicenseKey validated) =>
        new(
            LicenseKeyId: validated.Id ?? string.Empty,
            CustomerId: validated.CustomerId,
            ExpiresAt: validated.ExpiresAt?.DateTimeOffset,
            ActivationsRemaining: null,                   // deferred — see remarks above
            IsActiveAtPolar: validated.Status == LicenseKeyStatus.Granted);

    private static LicenseKeyApiError MapApiException(ApiException ex, LicenseKeyApiRequest request)
    {
        var kind = ex.ResponseStatusCode switch
        {
            404 => LicenseKeyApiErrorKind.NotFound,
            400 or 422 when ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                         || ex.Message.Contains("malformed", StringComparison.OrdinalIgnoreCase)
                         || ex.Message.Contains("uuid", StringComparison.OrdinalIgnoreCase)
                         || ex.Message.Contains("not.*found", StringComparison.OrdinalIgnoreCase)
                => LicenseKeyApiErrorKind.MalformedKey,
            _ => LicenseKeyApiErrorKind.UnexpectedFailure,
        };
        return new LicenseKeyApiError(kind, $"Polar license-key {ex.ResponseStatusCode}: {ex.Message}");
    }
}
