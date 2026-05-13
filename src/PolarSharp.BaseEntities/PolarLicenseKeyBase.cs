namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh license key. Issued as part of a benefit grant
/// (<see cref="PolarBenefitType.LicenseKeys"/>); validated at runtime via
/// <c>POST /v1/license-keys/{id}/validate</c>.
/// </summary>
public abstract record PolarLicenseKeyBase : IPolarEntity, IPolarTimestamped
{
    /// <summary>Gets the Polar.sh license-key identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the license-key string the customer activates with (e.g. "ACME-1A2B-3C4D-5E6F").</summary>
    public required string Key { get; init; }

    /// <summary>Gets the key's lifecycle status.</summary>
    public required PolarLicenseKeyStatus Status { get; init; }

    /// <summary>Gets the customer the key was issued to.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Gets the benefit identifier this key was issued under.</summary>
    public string? BenefitId { get; init; }

    /// <summary>Gets the UTC timestamp the customer activated the key (null when still inactive).</summary>
    public DateTimeOffset? ActivatedAt { get; init; }

    /// <summary>Gets the UTC timestamp the key expires (null = never expires).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the count of validations performed against the key.</summary>
    public int? UsageCount { get; init; }

    /// <summary>Gets the maximum allowed validations (null = unlimited).</summary>
    public int? UsageLimit { get; init; }

    /// <summary>Gets the UTC timestamp the key was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
