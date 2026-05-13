using System.Text.Json.Nodes;
using PolarSharp.BaseEntities;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement;

/// <summary>
/// Base for every locally-authored benefit. Concrete subtypes per Polar benefit kind —
/// polymorphism over discriminator strings (ZH rule 3).
/// </summary>
/// <remarks>
/// Inherits <see cref="PolarBenefitBase"/> from PolarSharp.BaseEntities so the host's domain
/// shape matches Polar's webhook wire format byte-for-byte where it overlaps.
/// </remarks>
public abstract record LocalBenefit : PolarBenefitBase, ITenantOwned, IFakeDataAware
{
    /// <summary>The local benefit identifier.</summary>
    public required BenefitId BenefitId { get; init; }

    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    public bool IsFakeData { get; init; }

    /// <summary>Display name shown in the Polar dashboard.</summary>
    public required string Name { get; init; }

    /// <summary>The Polar benefit id assigned on first publish. <see langword="null"/> until then.</summary>
    public string? PolarBenefitId { get; init; }

    /// <summary>UTC of the most-recent successful publish.</summary>
    public DateTimeOffset? LastPublishedAt { get; init; }

    /// <summary>Current publish status.</summary>
    public PublishStatus Status { get; init; } = PublishStatus.Draft;
}

/// <summary>A custom benefit — host-defined free-form properties.</summary>
public sealed record CustomBenefit : LocalBenefit
{
    /// <summary>Free-form JSON properties.</summary>
    public required JsonNode Properties { get; init; }
}

/// <summary>Polar's license_keys benefit — auto-generated keys delivered on purchase.</summary>
public sealed record LicenseKeysBenefit : LocalBenefit
{
    /// <summary>String prepended to every generated key (e.g. <c>"ACME-"</c>).</summary>
    public required string KeyPrefix { get; init; }

    /// <summary>Maximum number of distinct activations per key. <see langword="null"/> = unlimited.</summary>
    public int? MaxActivations { get; init; }

    /// <summary>UTC after which the keys cease to validate. <see langword="null"/> = no expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Key format.</summary>
    public required LicenseKeyFormat Format { get; init; }
}

/// <summary>Polar's downloadables benefit — files delivered after purchase.</summary>
public sealed record DownloadablesBenefit : LocalBenefit
{
    /// <summary>The set of files included.</summary>
    public required IReadOnlyList<DownloadableFile> Files { get; init; }
}

/// <summary>One file in a <see cref="DownloadablesBenefit"/>.</summary>
/// <param name="Name">Display name.</param>
/// <param name="MimeType">MIME type.</param>
/// <param name="SizeBytes">File size.</param>
/// <param name="PolarFileId">The Polar-side file id (<c>file_xxx</c>) returned after upload.</param>
public sealed record DownloadableFile(string Name, string MimeType, long SizeBytes, string PolarFileId);

/// <summary>Polar's github_repository benefit — invites the purchaser to a private repo.</summary>
public sealed record GitHubRepoBenefit : LocalBenefit
{
    /// <summary>The repo owner (organization or user).</summary>
    public required string RepoOwner { get; init; }

    /// <summary>The repo name.</summary>
    public required string RepoName { get; init; }

    /// <summary>Permission granted to the invitee.</summary>
    public required GitHubRepoPermission Permission { get; init; }
}

/// <summary>Polar's discord benefit — assigns a Discord role on purchase.</summary>
public sealed record DiscordRoleBenefit : LocalBenefit
{
    /// <summary>The Discord guild id.</summary>
    public required string GuildId { get; init; }

    /// <summary>The role id to assign within the guild.</summary>
    public required string RoleId { get; init; }
}

/// <summary>Polar's feature_flag benefit — exposes a queryable bag of feature flags to the customer.</summary>
public sealed record FeatureFlagBenefit : LocalBenefit
{
    /// <summary>Flag-name → value map. Queryable via Polar's customer-state API.</summary>
    public required IReadOnlyDictionary<string, string> Flags { get; init; }
}

/// <summary>Polar's meter_credit benefit — grants a quantity of metered units.</summary>
public sealed record MeterCreditBenefit : LocalBenefit
{
    /// <summary>The Polar meter id.</summary>
    public required string MeterId { get; init; }

    /// <summary>How many units to grant.</summary>
    public required long CreditUnits { get; init; }
}
