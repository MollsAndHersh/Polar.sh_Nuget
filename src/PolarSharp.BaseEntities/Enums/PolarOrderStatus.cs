using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Lifecycle status of a Polar.sh order. Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarOrderStatus
{
    /// <summary>The order has been created but payment has not yet been confirmed.</summary>
    [JsonStringEnumMemberName("pending")] Pending,

    /// <summary>Payment confirmed; the order is fulfillable.</summary>
    [JsonStringEnumMemberName("paid")] Paid,

    /// <summary>The order has been fully refunded.</summary>
    [JsonStringEnumMemberName("refunded")] Refunded,

    /// <summary>The order has been partially refunded; some amount remains paid.</summary>
    [JsonStringEnumMemberName("partially_refunded")] PartiallyRefunded,

    /// <summary>The order was voided before payment completed (e.g. checkout abandoned).</summary>
    [JsonStringEnumMemberName("void")] Void,
}
