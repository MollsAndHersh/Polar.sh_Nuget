namespace PolarSharp.Webhooks;

/// <summary>
/// Resolves per-tenant HMAC secrets and the tenant ID from a Polar organization ID
/// embedded in an incoming webhook payload.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by <c>PolarSharp.MultiTenant</c> when <c>AddPolarMultiTenant()</c> is
/// registered. When multi-tenancy is not in use, no implementation is registered and
/// <see cref="WebhookValidator"/> falls back to the global secrets in
/// <c>PolarSharp:Webhooks:Secrets</c>.
/// </para>
/// <para>
/// The resolver is called with the <c>organization_id</c> extracted from the raw
/// (unverified) request body — the value is used ONLY to select the correct HMAC secret.
/// No business logic may act on unverified data.
/// </para>
/// </remarks>
public interface IWebhookTenantResolver
{
    /// <summary>
    /// Returns the HMAC secrets to try for the given Polar organization ID and the
    /// matching tenant identifier, or <see langword="null"/> if no tenant is registered
    /// for that organization.
    /// </summary>
    /// <param name="polarOrganizationId">
    /// The <c>organization_id</c> pre-parsed from the raw webhook payload bytes before
    /// HMAC verification — used solely for secret selection.
    /// </param>
    /// <returns>
    /// A <see cref="WebhookTenantResolution"/> containing the HMAC secrets to attempt
    /// and the tenant's identifier string, or <see langword="null"/> if the organization
    /// ID does not match any registered tenant.
    /// </returns>
    WebhookTenantResolution? Resolve(string polarOrganizationId);
}

/// <summary>
/// The result of resolving a tenant from a Polar organization ID.
/// </summary>
/// <param name="Secrets">
/// The HMAC secrets to attempt for this tenant. Typically contains the current
/// active secret plus any secrets retained during key rotation.
/// </param>
/// <param name="TenantId">
/// The Finbuckle tenant identifier string for the resolved tenant. This is the
/// value stored in <c>ITenantInfo.Id</c> — store GUID strings here.
/// </param>
public sealed record WebhookTenantResolution(IReadOnlyList<string> Secrets, string TenantId);
