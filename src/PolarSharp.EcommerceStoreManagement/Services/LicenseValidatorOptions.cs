namespace PolarSharp.EcommerceStoreManagement.Services;

/// <summary>
/// Configuration for <see cref="ILicenseKeyValidator"/>. Bound from
/// <c>PolarSharp:EcommerceStoreManagement:LicenseValidation</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class LicenseValidatorOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "PolarSharp:EcommerceStoreManagement:LicenseValidation";

    /// <summary>
    /// How long a successful validation is cached in <c>IMemoryCache</c> before the next
    /// request re-validates against Polar. Defaults to 60 seconds. Set to 0 to disable
    /// caching entirely (every request hits Polar).
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 60;

    /// <summary>
    /// Number of days after expiration that an expired key still validates as
    /// <see cref="LicenseValidationResult.IsValid"/> = <see langword="true"/> AND
    /// <see cref="LicenseValidationResult.IsWithinGracePeriod"/> = <see langword="true"/>.
    /// The host can show a "license expired — please renew" banner during the grace window
    /// without revoking access. Defaults to 7 days.
    /// </summary>
    public int GracePeriodDays { get; set; } = 7;
}
