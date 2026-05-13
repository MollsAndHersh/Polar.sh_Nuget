namespace PolarSharp.EcommerceStoreManagement.Cloning;

/// <summary>Clones a <see cref="LocalBenefit"/>. The polymorphic subtype is preserved — a <c>LicenseKeysBenefit</c> clones to a <c>LicenseKeysBenefit</c>, etc.</summary>
/// <remarks>
/// <para>
/// <strong>Duplicate prevention:</strong> the new <c>Name</c> is auto-suffixed with
/// <c>" (Copy)"</c>. Polar-side state (<c>PolarBenefitId</c>, <c>LastPublishedAt</c>,
/// <c>Status</c>) is reset to a fresh <c>Draft</c>.
/// </para>
/// <para>
/// <strong>License-key benefit clones</strong> reset any <c>MaxActivations</c> / expiry
/// timer to fresh values — the clone is a brand-new benefit, NOT a copy of the source's
/// remaining activations. Same for <c>MeterCreditBenefit</c>: <c>CreditUnits</c> copies but
/// the activation history doesn't.
/// </para>
/// </remarks>
public interface IBenefitCloningService
{
    /// <summary>Clones the benefit.</summary>
    Task<Result<LocalBenefit, CloningError>> CloneAsync(
        BenefitId source,
        CloneBenefitOverrides? overrides = null,
        CloneBenefitOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>Field overrides for a benefit clone.</summary>
public sealed record CloneBenefitOverrides
{
    /// <summary>Override the new benefit's display name.</summary>
    public string? NewName { get; init; }
    /// <summary>Override the new benefit's description.</summary>
    public string? NewDescription { get; init; }
}

/// <summary>Cascade toggles for a benefit clone.</summary>
public sealed record CloneBenefitOptions
{
    /// <summary>When <see langword="true"/>, translation rows are duplicated. Default <see langword="true"/>.</summary>
    public bool IncludeTranslations { get; init; } = true;
}
