using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Recurring billing cadence for subscription products. <see cref="None"/> indicates a
/// one-time (non-recurring) product. Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarRecurringInterval
{
    /// <summary>Not a recurring product; one-time purchase.</summary>
    [JsonStringEnumMemberName("none")] None,

    /// <summary>Bills every week.</summary>
    [JsonStringEnumMemberName("weekly")] Weekly,

    /// <summary>Bills every month.</summary>
    [JsonStringEnumMemberName("monthly")] Monthly,

    /// <summary>Bills every three months.</summary>
    [JsonStringEnumMemberName("quarterly")] Quarterly,

    /// <summary>Bills every six months.</summary>
    [JsonStringEnumMemberName("biannual")] Biannual,

    /// <summary>Bills every year.</summary>
    [JsonStringEnumMemberName("yearly")] Yearly,
}
