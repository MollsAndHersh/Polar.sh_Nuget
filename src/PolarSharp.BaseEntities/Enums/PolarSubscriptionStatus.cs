using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Lifecycle status of a Polar.sh subscription. Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarSubscriptionStatus
{
    /// <summary>Subscription created but initial payment not yet completed.</summary>
    [JsonStringEnumMemberName("incomplete")] Incomplete,

    /// <summary>Subscription was incomplete and the grace window for payment has expired.</summary>
    [JsonStringEnumMemberName("incomplete_expired")] IncompleteExpired,

    /// <summary>Subscription is in a free trial period; no charges yet.</summary>
    [JsonStringEnumMemberName("trialing")] Trialing,

    /// <summary>Subscription is active and billing on schedule.</summary>
    [JsonStringEnumMemberName("active")] Active,

    /// <summary>Most recent renewal payment failed; subscription remains active during grace window.</summary>
    [JsonStringEnumMemberName("past_due")] PastDue,

    /// <summary>Subscription has been canceled by the customer or the merchant.</summary>
    [JsonStringEnumMemberName("canceled")] Canceled,

    /// <summary>Renewal payment failed past the grace window; benefits revoked.</summary>
    [JsonStringEnumMemberName("unpaid")] Unpaid,
}
