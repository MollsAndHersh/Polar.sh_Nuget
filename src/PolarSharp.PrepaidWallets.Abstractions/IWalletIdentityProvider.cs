namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Mode-agnostic identity provider for the wallet feature (per Case Study 05
/// "Multi-Tenancy as Optional for Library Authors"). Bridges implement this against
/// the host's actual identity infrastructure — PolarSharp.MultiTenant.Identity in
/// multi-tenant mode; ASP.NET Core Identity directly in single-tenant mode.
/// </summary>
public interface IWalletIdentityProvider
{
    /// <summary>Who is performing this operation. Required.</summary>
    Guid CurrentUserId { get; }

    /// <summary>The tenant scope; <see langword="null"/> in single-tenant deployments.</summary>
    Guid? CurrentTenantId { get; }

    /// <summary>True ⇒ multi-tenant-aware host; false ⇒ single-tenant host. Drives query-filter behavior + audience-tier collapse.</summary>
    bool IsMultiTenantMode { get; }
}

/// <summary>The user audiences served by the wallet feature.</summary>
public enum WalletAudienceScope
{
    /// <summary>
    /// Single-tenant: the host's full-access operator.
    /// Multi-tenant: collapses to SaaSAdmin (cross-tenant ops require explicit opt-in via <c>[AllowCrossTenant]</c>).
    /// </summary>
    HostOperator,

    /// <summary>Multi-tenant only: tenant-scoped operator. In single-tenant deployments, this scope is invalid and resolves to HostOperator.</summary>
    Tenant,

    /// <summary>Either mode: end-customer (own wallet only).</summary>
    Customer,
}
