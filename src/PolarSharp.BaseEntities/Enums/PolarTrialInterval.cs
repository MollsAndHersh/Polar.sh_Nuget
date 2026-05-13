using System.Text.Json.Serialization;

namespace PolarSharp.BaseEntities;

/// <summary>
/// Time-unit for a subscription product's free-trial period. Combined with a count to express
/// e.g. "14 days" (Days × 14). Wire-format strings match Polar's API exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PolarTrialInterval
{
    /// <summary>Trial period measured in days.</summary>
    [JsonStringEnumMemberName("days")] Days,

    /// <summary>Trial period measured in weeks.</summary>
    [JsonStringEnumMemberName("weeks")] Weeks,

    /// <summary>Trial period measured in months.</summary>
    [JsonStringEnumMemberName("months")] Months,
}
