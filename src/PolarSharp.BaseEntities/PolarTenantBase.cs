namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh organization (a merchant tenant). Every PolarSharp tenant
/// representation — and any host application's tenant type — should inherit from this base
/// to share Polar's wire format.
/// </summary>
/// <remarks>
/// <para>
/// Polar emits this shape via the <c>/v1/organizations/{id}</c> endpoint and on every
/// authenticated webhook delivery (the <c>organization_id</c> field references this entity).
/// </para>
/// <para>
/// <see cref="AccountId"/> and <see cref="PayoutAccountId"/> are READ-ONLY from Polar's side —
/// they're populated when the merchant completes Stripe Connect linking via Polar's web
/// dashboard. PolarSharp can only OBSERVE these values; it cannot set them programmatically.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyTenant : PolarTenantBase
/// {
///     public string MyInternalReference { get; init; } = "";
/// }
/// </code>
/// </example>
public abstract record PolarTenantBase : IPolarEntity, IPolarTimestamped
{
    /// <summary>Gets the Polar.sh organization identifier (a Polar-assigned string).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the merchant's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the URL-safe slug Polar uses in dashboard URLs (e.g. "acme-corp" in <c>https://polar.sh/acme-corp</c>).</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the merchant's country code (ISO 3166-1 alpha-2).</summary>
    public string? Country { get; init; }

    /// <summary>Gets the merchant's primary contact email.</summary>
    public string? Email { get; init; }

    /// <summary>Gets the merchant's website URL.</summary>
    public string? Website { get; init; }

    /// <summary>Gets the URL of the merchant's avatar / logo image.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Gets the merchant's default presentment currency (ISO 4217, e.g. "USD", "EUR").</summary>
    public string? DefaultPresentmentCurrency { get; init; }

    /// <summary>Gets the organization's lifecycle status.</summary>
    public PolarOrganizationStatus Status { get; init; } = PolarOrganizationStatus.Active;

    /// <summary>Gets the linked Stripe Connect account identifier. READ-ONLY — set by Polar when the merchant completes Connect onboarding via Polar's dashboard.</summary>
    public string? AccountId { get; init; }

    /// <summary>Gets the linked payout account identifier. READ-ONLY — set by Polar when payouts are configured.</summary>
    public string? PayoutAccountId { get; init; }

    /// <summary>Gets the UTC timestamp the organization was created in Polar.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the organization was last modified in Polar.</summary>
    public DateTimeOffset? ModifiedAt { get; init; }
}
