namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh benefit grant — a customer's claim on a specific benefit
/// (e.g. "this customer has been granted Discord-role-X via this benefit").
/// </summary>
/// <remarks>
/// Property shape matches the <c>benefit_grant.created</c> / <c>.updated</c> / <c>.revoked</c>
/// webhook payloads exactly.
/// </remarks>
public abstract record PolarBenefitGrantBase : IPolarEntity, IPolarTimestamped, IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh benefit grant identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the customer the benefit was granted to.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Gets the benefit identifier.</summary>
    public required string BenefitId { get; init; }

    /// <summary>Gets the benefit type discriminator (mirrors <see cref="PolarBenefitBase.Type"/>).</summary>
    public required PolarBenefitType BenefitType { get; init; }

    /// <summary>Gets a value indicating whether the grant is currently active.</summary>
    public bool IsGranted { get; init; }

    /// <summary>Gets the UTC timestamp the benefit was granted.</summary>
    public DateTimeOffset? GrantedAt { get; init; }

    /// <summary>Gets the UTC timestamp the benefit was revoked (null when still active).</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Gets the Polar organization the grant belongs to.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets the UTC timestamp the grant record was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
