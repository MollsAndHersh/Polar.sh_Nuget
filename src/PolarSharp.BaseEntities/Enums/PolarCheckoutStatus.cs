using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Lifecycle status of a Polar.sh checkout session. Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarCheckoutStatus
{
    /// <summary>Checkout is open and the customer can complete it.</summary>
    [JsonStringEnumMemberName("open")] Open,

    /// <summary>Checkout's <c>expires_at</c> has passed without confirmation.</summary>
    [JsonStringEnumMemberName("expired")] Expired,

    /// <summary>Customer completed the checkout and an order was created.</summary>
    [JsonStringEnumMemberName("confirmed")] Confirmed,
}
