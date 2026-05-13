using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Lifecycle status of a Polar.sh license key. Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarLicenseKeyStatus
{
    /// <summary>License key is valid and may be used.</summary>
    [JsonStringEnumMemberName("active")] Active,

    /// <summary>License key has not yet been activated by the customer.</summary>
    [JsonStringEnumMemberName("inactive")] Inactive,

    /// <summary>License key has been disabled (revoked, expired, or manually deactivated).</summary>
    [JsonStringEnumMemberName("disabled")] Disabled,
}
