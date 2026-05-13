using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Lifecycle status of a Polar.sh organization (a merchant tenant). Wire-format strings match
/// Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarOrganizationStatus
{
    /// <summary>Organization is active and may transact.</summary>
    [JsonStringEnumMemberName("active")] Active,

    /// <summary>Organization is awaiting Polar's compliance / KYC review.</summary>
    [JsonStringEnumMemberName("pending_review")] PendingReview,

    /// <summary>Organization has been suspended (compliance issue, fraud signal, manual action).</summary>
    [JsonStringEnumMemberName("suspended")] Suspended,
}
