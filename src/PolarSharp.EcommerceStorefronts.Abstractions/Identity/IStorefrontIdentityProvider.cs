namespace PolarSharp.EcommerceStorefronts.Abstractions.Identity;

/// <summary>
/// Mode-agnostic identity provider for the storefront feature (Case Study 05
/// "Multi-Tenancy as Optional for Library Authors").
/// </summary>
/// <remarks>
/// Bridges adapt this against the host's actual identity infrastructure —
/// <c>PolarSharp.MultiTenant.Identity</c> in multi-tenant mode, ASP.NET Core Identity
/// (or the host's bespoke layer) in single-tenant mode. Mirrors the
/// <c>IWalletIdentityProvider</c> pattern from <c>PolarSharp.PrepaidWallets.Abstractions</c>:
/// the customer / tenant id is optional (guests + single-tenant hosts), the mode is
/// surfaced as a flag, and the bridge layer is the only place that knows whether
/// the host is multi-tenant.
/// <para>
/// Lift-safe: no dependency on any <c>PolarSharp.*</c> assembly outside
/// <c>PolarSharp.BaseEntities</c> + the storefront-core family.
/// </para>
/// </remarks>
public interface IStorefrontIdentityProvider
{
    /// <summary>
    /// The current customer's identifier. <see cref="StorefrontOption{T}.None"/>
    /// for guest customers; populated when the customer is signed in.
    /// </summary>
    StorefrontOption<Guid> CurrentCustomerId { get; }

    /// <summary>
    /// The current tenant's identifier. <see cref="StorefrontOption{T}.None"/>
    /// in single-tenant deployments; populated by the multi-tenant bridge.
    /// </summary>
    StorefrontOption<Guid> CurrentTenantId { get; }

    /// <summary>
    /// True when the host is multi-tenant. Drives query-filter and audience-scope
    /// behaviour throughout the storefront stack.
    /// </summary>
    bool IsMultiTenantMode { get; }

    /// <summary>
    /// True when a customer is signed in. Equivalent to
    /// <c>CurrentCustomerId.HasValue</c>; surfaced as a flag so call sites avoid
    /// re-deriving the boolean.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// True when there is no signed-in customer. Inverse of <see cref="IsAuthenticated"/>;
    /// surfaced explicitly so call sites read naturally.
    /// </summary>
    bool IsGuest { get; }
}
