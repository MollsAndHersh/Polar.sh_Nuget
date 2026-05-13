using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Categorical reason a Polar.sh refund was issued. Drives Polar's risk reporting and the
/// merchant's audit trail. Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarRefundReason
{
    /// <summary>Customer was charged twice for the same item.</summary>
    [JsonStringEnumMemberName("duplicate")] Duplicate,

    /// <summary>Charge was made by an unauthorized party (e.g. stolen card).</summary>
    [JsonStringEnumMemberName("fraudulent")] Fraudulent,

    /// <summary>Customer asked for a refund (no fault on the merchant's side).</summary>
    [JsonStringEnumMemberName("customer_request")] CustomerRequest,

    /// <summary>The merchant's service was disrupted and the customer was charged anyway.</summary>
    [JsonStringEnumMemberName("service_disruption")] ServiceDisruption,

    /// <summary>The merchant offers a satisfaction guarantee and the customer invoked it.</summary>
    [JsonStringEnumMemberName("satisfaction_guarantee")] SatisfactionGuarantee,

    /// <summary>The merchant proactively refunded to prevent a chargeback dispute.</summary>
    [JsonStringEnumMemberName("dispute_prevention")] DisputePrevention,

    /// <summary>None of the above; reason is opaque or operator-defined.</summary>
    [JsonStringEnumMemberName("other")] Other,
}
