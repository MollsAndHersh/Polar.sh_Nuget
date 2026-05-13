using Microsoft.Extensions.Logging;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IPolarOrganizationsApi"/> implementation backed by the Kiota
/// <see cref="PolarClient"/>. Best-effort wiring — see TASK-V20-004 for sandbox validation.
/// </summary>
internal sealed class PolarClientOrganizationsApi(PolarClient polar, ILogger<PolarClientOrganizationsApi> logger) : IPolarOrganizationsApi
{
    private readonly PolarClient _polar = polar ?? throw new ArgumentNullException(nameof(polar));
    private readonly ILogger<PolarClientOrganizationsApi> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<Result<OrganizationApiResponse, OrganizationApiError>> UpdateAsync(string polarOrganizationId, OrganizationUpdateRequest request, CancellationToken ct)
    {
        // TODO TASK-V20-004: wire to _polar.V1.Organizations.Item[orgId].PatchAsync(OrganizationUpdate, ct)
        // mapping OrganizationUpdate <- OrganizationUpdateRequest, OrganizationApiResponse <- Organization.
        _logger.LogWarning(
            "PolarClientOrganizationsApi.UpdateAsync called for org {OrganizationId} but HTTP wiring is deferred to TASK-V20-004 — returning UnexpectedFailure.",
            polarOrganizationId);
        return Task.FromResult(Result<OrganizationApiResponse, OrganizationApiError>.Failure(new OrganizationApiError(
            OrganizationApiErrorKind.UnexpectedFailure,
            "Polar organization-update HTTP wiring is deferred to TASK-V20-004 — supply a custom IPolarOrganizationsApi implementation or wait for the sandbox-validated wrapper.")));
    }

    public Task<Result<OrganizationApiResponse, OrganizationApiError>> GetAsync(string polarOrganizationId, CancellationToken ct)
    {
        // TODO TASK-V20-004: wire to _polar.V1.Organizations.Item[orgId].GetAsync(ct).
        _logger.LogWarning(
            "PolarClientOrganizationsApi.GetAsync called for org {OrganizationId} but HTTP wiring is deferred to TASK-V20-004 — returning UnexpectedFailure.",
            polarOrganizationId);
        return Task.FromResult(Result<OrganizationApiResponse, OrganizationApiError>.Failure(new OrganizationApiError(
            OrganizationApiErrorKind.UnexpectedFailure,
            "Polar organization-get HTTP wiring is deferred to TASK-V20-004.")));
    }
}
