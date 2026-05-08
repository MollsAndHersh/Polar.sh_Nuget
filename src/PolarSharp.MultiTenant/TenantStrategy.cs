namespace PolarSharp.MultiTenant;

/// <summary>
/// Specifies the HTTP request strategy used to identify the current tenant.
/// </summary>
public enum TenantStrategy
{
    /// <summary>
    /// Resolves the tenant from a request header (default).
    /// Configured by <see cref="PolarMultiTenantOptions.Header"/>.
    /// </summary>
    Header,

    /// <summary>
    /// Resolves the tenant from a route parameter.
    /// Configured by <see cref="PolarMultiTenantOptions.Route"/>.
    /// </summary>
    Route,

    /// <summary>
    /// Resolves the tenant from the request hostname using a template pattern.
    /// Configured by <see cref="PolarMultiTenantOptions.Hostname"/>.
    /// </summary>
    Hostname,

    /// <summary>
    /// Resolves the tenant from a JWT claim on the authenticated principal.
    /// Configured by <see cref="PolarMultiTenantOptions.Claim"/>.
    /// </summary>
    Claim,
}
