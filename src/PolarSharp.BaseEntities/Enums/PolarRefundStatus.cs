using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Lifecycle status of a Polar.sh refund. Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarRefundStatus
{
    /// <summary>Refund initiated; funds in flight back to the customer.</summary>
    [JsonStringEnumMemberName("pending")] Pending,

    /// <summary>Refund completed successfully; customer has received the funds.</summary>
    [JsonStringEnumMemberName("succeeded")] Succeeded,

    /// <summary>Refund failed (insufficient funds, customer card closed, etc.).</summary>
    [JsonStringEnumMemberName("failed")] Failed,
}
