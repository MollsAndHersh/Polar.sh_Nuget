using PolarSharp.EcommerceStoreManagement.Services;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Minimal Polar HTTP boundary for the Organization resource — wraps the Kiota-generated
/// <c>PolarClient.V1.Organizations</c> calls behind a typed interface so
/// <see cref="PolarBusinessProfileService"/> can be unit-tested with a fake API.
/// </summary>
/// <remarks>
/// The real implementation (<c>PolarClientOrganizationsApi</c>) is best-effort and not yet
/// sandbox-validated — see TASK-V20-004 in <c>~/TASKS.md</c>.
/// </remarks>
internal interface IPolarOrganizationsApi
{
    /// <summary>PATCH the writable subset of an organization (country, currency, tax behaviour, KYC details).</summary>
    Task<Result<OrganizationApiResponse, OrganizationApiError>> UpdateAsync(string polarOrganizationId, OrganizationUpdateRequest request, CancellationToken ct);

    /// <summary>GET the current organization payload — used by the payout-status poll.</summary>
    Task<Result<OrganizationApiResponse, OrganizationApiError>> GetAsync(string polarOrganizationId, CancellationToken ct);
}

/// <summary>Writable subset of the organization shape PATCH'd to Polar.</summary>
internal sealed record OrganizationUpdateRequest(
    string? Country,
    string? DefaultPresentmentCurrency,
    DefaultTaxBehavior TaxBehavior,
    string? ProductDescription,
    string? IntendedUse,
    IReadOnlyList<string> PricingModels,
    IReadOnlyList<string> SellingCategories,
    long? FutureAnnualRevenue,
    string? SwitchingFrom);

/// <summary>Polar's organization response — only the fields the business profile mirrors.</summary>
internal sealed record OrganizationApiResponse(
    string Id,
    string? Country,
    string? DefaultPresentmentCurrency,
    string? AccountId,
    string? PayoutAccountId);

/// <summary>Polar-side organization-error discriminator.</summary>
internal sealed record OrganizationApiError(OrganizationApiErrorKind Kind, string Message);

/// <summary>Polar-side organization failure modes.</summary>
internal enum OrganizationApiErrorKind
{
    NotFound,
    ValidationFailed,
    UnexpectedFailure,
}
