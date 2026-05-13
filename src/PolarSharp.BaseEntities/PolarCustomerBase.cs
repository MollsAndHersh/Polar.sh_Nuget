namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh customer (the payer; distinct from the SaaS host's own
/// application user). Every PolarSharp customer representation — and any host application's
/// customer type — should inherit from this base to share Polar's wire format.
/// </summary>
/// <remarks>
/// <para>
/// Polar emits this shape via <c>/v1/customers/{id}</c> and on every customer-scoped webhook
/// (<c>customer.created</c>, <c>customer.updated</c>, <c>customer.state_changed</c>,
/// <c>customer.deleted</c>).
/// </para>
/// <para>
/// <see cref="ExternalId"/> is the host's own customer identifier — a foreign-key linking the
/// Polar customer record to the host's user/customer system.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record MyShopCustomer : PolarCustomerBase
/// {
///     public string LoyaltyTier { get; init; } = "Bronze";
///     public int LoyaltyPoints { get; init; }
/// }
/// </code>
/// </example>
public abstract record PolarCustomerBase : IPolarEntity, IPolarTimestamped, IPolarMetadata, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh customer identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the customer's email address.</summary>
    public required string Email { get; init; }

    /// <summary>Gets the customer's display name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the host's own identifier for this customer (foreign-key to the host's user/customer system).</summary>
    public string? ExternalId { get; init; }

    /// <summary>Gets the customer's billing address (used for tax calculation and invoicing).</summary>
    public PolarAddressBase? BillingAddress { get; init; }

    /// <summary>Gets the URL of the customer's avatar image.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Gets a value indicating whether the customer's email has been verified.</summary>
    public bool EmailVerified { get; init; }

    /// <summary>Gets the customer's preferred locale (e.g. "en-US", "es-MX").</summary>
    public string? Locale { get; init; }

    /// <summary>Gets the Polar organization the customer belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets the customer's free-form metadata key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the UTC timestamp the customer was created in Polar.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the customer was last modified in Polar.</summary>
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>Gets the UTC timestamp the customer was soft-deleted in Polar (null when active).</summary>
    public DateTimeOffset? DeletedAt { get; init; }
}
