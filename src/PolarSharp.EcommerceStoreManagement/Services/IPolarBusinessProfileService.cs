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
    /// Returns a clickable link the merchant can follow to set up their bank account
    /// for receiving payouts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Important — PolarSharp does NOT talk to Stripe.</b> The merchant connects their
    /// bank account through the Polar.sh website's own admin dashboard, not through
    /// PolarSharp or your application. Polar.sh uses Stripe behind the scenes to move
    /// money to the merchant's bank, so the merchant may see Stripe-branded screens
    /// while they're completing the setup in Polar's dashboard — that is normal and
    /// expected, and it does not mean your application has any direct relationship with
    /// Stripe.
    /// </para>
    /// <para>
    /// Your application's only job is to show this link somewhere in your admin UI
    /// (typically with text like "Connect your bank account to receive payouts") and let
    /// the merchant click through to Polar's site. PolarSharp cannot do that step
    /// programmatically because Polar's API has no endpoint for it — the merchant must
    /// complete it manually in a browser.
    /// </para>
    /// <para>
    /// After the merchant finishes setting up their bank in Polar's dashboard, your
    /// application can call <see cref="RefreshPayoutStatusAsync"/> (or rely on the
    /// background <c>PayoutStatusPollerService</c> when enabled) to find out whether
    /// Polar reports the setup as complete. When complete, the merchant is ready to
    /// receive payouts.
    /// </para>
    /// </remarks>
    Uri BuildBankingSetupDeepLink();

    /// <summary>
    /// Asks Polar.sh whether the merchant has finished setting up their bank account for
    /// payouts, and updates the local profile to match.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PolarSharp calls Polar's <c>GET /v1/organizations/{id}</c> endpoint. The response
    /// contains two fields — <c>account_id</c> and <c>payout_account_id</c> — that Polar
    /// fills in once the merchant has finished the bank-account setup in Polar's
    /// dashboard. These fields are <b>read-only</b> from Polar's side; PolarSharp only
    /// mirrors them into the local profile so your application can show "Payouts ready"
    /// in the UI without having to call Polar on every page load.
    /// </para>
    /// <para>
    /// <b>PolarSharp does NOT contact Stripe.</b> The values <c>account_id</c> and
    /// <c>payout_account_id</c> happen to be Stripe identifiers because Polar.sh uses
    /// Stripe Connect internally for payouts, but PolarSharp treats them as opaque
    /// status indicators — it never makes any HTTP call to Stripe directly.
    /// </para>
    /// </remarks>
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
