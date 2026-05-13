namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh benefit (an entitlement granted on purchase). Polar
/// supports seven benefit types — see <see cref="PolarBenefitType"/>.
/// </summary>
/// <remarks>
/// <para>
/// Type-specific configuration (license-key prefix, downloadable file references, GitHub
/// repo + permission, Discord guild + role, feature-flag dictionary, meter id + credit units,
/// custom JSON properties) lives on derived records in PolarSharp.EcommerceStoreManagement —
/// each benefit kind has its own sealed-record subclass there.
/// </para>
/// </remarks>
public abstract record PolarBenefitBase : IPolarEntity, IPolarTimestamped, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh benefit identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the benefit type discriminator.</summary>
    public required PolarBenefitType Type { get; init; }

    /// <summary>Gets the customer-facing description of the benefit.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the Polar organization the benefit belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets a value indicating whether the benefit is subject to sales tax.</summary>
    public bool IsTaxApplicable { get; init; }

    /// <summary>Gets a value indicating whether the customer can opt in/out of receiving the benefit at checkout.</summary>
    public bool Selectable { get; init; }

    /// <summary>Gets a value indicating whether the benefit can be deleted (false for benefits with active grants).</summary>
    public bool Deletable { get; init; }

    /// <summary>Gets the UTC timestamp the benefit was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
