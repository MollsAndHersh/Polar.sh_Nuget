namespace PolarSharp.EcommerceStoreManagement.Services;

/// <summary>
/// Reads and writes the tenant's <see cref="TenantBusinessProfile"/>. Writes that touch
/// Polar-owned fields (country, currency, tax behaviour, KYC details) are pushed to Polar
/// inline via the Organization PATCH endpoint; locally-only fields (street address, KYC
/// extras, per-tenant translation config) stay in the local SQL row.
/// </summary>
public interface IPolarBusinessProfileService
{
    /// <summary>Returns the current tenant's business profile.</summary>
    Task<Result<TenantBusinessProfile, BusinessProfileError>> GetAsync(CancellationToken ct = default);

    /// <summary>Persists the supplied profile. Local fields are saved unconditionally; Polar-writable fields are pushed inline.</summary>
    Task<Result<TenantBusinessProfile, BusinessProfileError>> SaveAsync(
        TenantBusinessProfile profile,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a deep-link URL the merchant can follow to complete Stripe Connect onboarding
    /// in the Polar dashboard. Polar's API exposes no programmatic Connect flow — the host
    /// surfaces this URL in their admin UI and polls <see cref="RefreshPayoutStatusAsync"/>
    /// to detect completion.
    /// </summary>
    Uri BuildBankingSetupDeepLink();

    /// <summary>
    /// Polls Polar's <c>GET /v1/organizations/{id}</c> and updates the local profile's
    /// <c>AccountId</c> / <c>PayoutAccountId</c> / <see cref="PayoutSetupStatus"/> from the
    /// response.
    /// </summary>
    Task<Result<PayoutSetupStatus, BusinessProfileError>> RefreshPayoutStatusAsync(CancellationToken ct = default);
}

/// <summary>Recoverable business-profile failure modes.</summary>
public sealed record BusinessProfileError(BusinessProfileErrorKind Kind, string Message);

/// <summary>Discriminator for business-profile errors.</summary>
public enum BusinessProfileErrorKind
{
    /// <summary>No profile exists yet for this tenant.</summary>
    NotFound,
    /// <summary>Polar rejected one or more fields (validation).</summary>
    PolarValidation,
    /// <summary>Polar API failure (5xx, timeout).</summary>
    PolarApiFailure,
}
