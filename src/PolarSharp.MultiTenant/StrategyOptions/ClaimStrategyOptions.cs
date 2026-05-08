namespace PolarSharp.MultiTenant.StrategyOptions;

/// <summary>
/// Configuration for the JWT-claim-based tenant resolution strategy.
/// </summary>
public class ClaimStrategyOptions
{
    /// <summary>
    /// Gets or sets the JWT claim type used to identify the tenant.
    /// </summary>
    /// <value>Default: <c>tid</c> (tenant ID claim used by Azure AD and many OIDC providers).</value>
    /// <remarks>Bound from <c>PolarSharp:MultiTenant:Claim:Type</c>.</remarks>
    public string Type { get; set; } = "tid";
}
