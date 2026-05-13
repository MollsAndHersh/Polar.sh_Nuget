using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// The seven kinds of benefit Polar.sh supports for entitlement-grant flows. Wire-format
/// strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarBenefitType
{
    /// <summary>A free-form benefit with arbitrary host-defined properties.</summary>
    [JsonStringEnumMemberName("custom")] Custom,

    /// <summary>Grants a Discord role on a configured server.</summary>
    [JsonStringEnumMemberName("discord")] Discord,

    /// <summary>Grants access to one or more downloadable files.</summary>
    [JsonStringEnumMemberName("downloadables")] Downloadables,

    /// <summary>Grants a feature flag set queryable via Polar's customer-state API.</summary>
    [JsonStringEnumMemberName("feature_flag")] FeatureFlag,

    /// <summary>Grants access to a GitHub repository at a specified permission level.</summary>
    [JsonStringEnumMemberName("github_repository")] GithubRepository,

    /// <summary>Issues a license key the customer can activate.</summary>
    [JsonStringEnumMemberName("license_keys")] LicenseKeys,

    /// <summary>Credits a metered usage meter with a fixed quantity of units.</summary>
    [JsonStringEnumMemberName("meter_credit")] MeterCredit,
}
