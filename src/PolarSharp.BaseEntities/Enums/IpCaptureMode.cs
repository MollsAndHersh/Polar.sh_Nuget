using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Per-tenant policy governing capture of customer IP addresses for fraud detection
/// and forensic analysis. Each tenant chooses one of three modes via
/// <c>TenantBusinessProfile.IpCaptureMode</c>; the chosen mode applies to every
/// customer-bound operation that flows through PolarSharp services (refunds, license
/// validation, checkout, onboarding callback, wallet operations, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Defaults to <see cref="Disabled"/> for new tenants — the strictest privacy posture.
/// Tenants who want fraud-detection capability opt in to one of the other modes.
/// </para>
/// <para>
/// <strong>Jurisdictional caveat</strong>: IP-address handling is regulated in many
/// jurisdictions (GDPR, CCPA, etc.). The PolarSharp wallet ships a per-jurisdiction
/// advisory (<c>WalletJurisdictionAdvisory</c>) that emits Warning logs when the
/// tenant's <c>CountryCode</c> + chosen mode appears non-compliant. The advisory is
/// informational only — tenants retain legal-compliance responsibility.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IpCaptureMode
{
    /// <summary>
    /// No IP / UA stored anywhere. Default for new tenants. Strongest privacy posture.
    /// Suitable for tenants under strict consumer-protection regimes OR tenants who
    /// don't need fraud-detection capability.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// IP address is hashed via SHA-256 with a per-tenant salt, then base64-encoded
    /// + truncated to 22 chars (132 bits of entropy). Stored as the hash only;
    /// raw IP is unrecoverable. Within a tenant, the same IP always hashes to the
    /// same value (intentional — fraud-detection-via-hash-equality works); per-tenant
    /// salts prevent cross-tenant correlation attacks.
    /// </summary>
    /// <remarks>
    /// 132 bits is well above the birthday-bound collision threshold for any realistic
    /// IP population, AND it's small enough to index efficiently in a database column.
    /// </remarks>
    CaptureHashed = 1,

    /// <summary>
    /// Raw IP address stored verbatim. Subject to the per-tenant retention policy
    /// (default 90 days; <c>IpRetentionPrunerService</c> nullifies entries past the
    /// retention window). Stronger fraud-detection capability than hashed mode
    /// (geolocation, network analysis, etc.) but with the corresponding privacy +
    /// regulatory burden.
    /// </summary>
    CaptureRaw = 2,
}
