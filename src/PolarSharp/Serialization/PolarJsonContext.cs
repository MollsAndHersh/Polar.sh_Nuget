using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolarSharp.Serialization;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for PolarSharp public model types.
/// </summary>
/// <remarks>
/// <para>
/// Enables Native AOT-compatible JSON serialization without reflection.
/// Kiota-generated models in <c>PolarSharp.Generated</c> use their own internal serialization
/// pipeline — this context covers PolarSharp's own public types (error records, toast
/// notifications) where STJ is used directly.
/// </para>
/// <para>
/// Configured with:
/// <list type="bullet">
///   <item><c>MaxDepth = 32</c> — prevents JSON bomb / stack overflow attacks.</item>
///   <item><c>AllowTrailingCommas = false</c> — strict JSON compliance.</item>
///   <item><c>ReadCommentHandling = Disallow</c> — strict JSON compliance.</item>
///   <item><c>PropertyNamingPolicy = CamelCase</c> — matches Polar's wire format.</item>
///   <item><c>DefaultIgnoreCondition = WhenWritingNull</c> — omit null fields in requests.</item>
/// </list>
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    MaxDepth = 32,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(global::PolarSharp.AuthenticationError))]
[JsonSerializable(typeof(global::PolarSharp.AuthorizationError))]
[JsonSerializable(typeof(global::PolarSharp.NotFoundError))]
[JsonSerializable(typeof(global::PolarSharp.ValidationError))]
[JsonSerializable(typeof(global::PolarSharp.RateLimitError))]
[JsonSerializable(typeof(global::PolarSharp.ServerError))]
[JsonSerializable(typeof(global::PolarSharp.FieldValidationError))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
public partial class PolarJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Gets the shared <see cref="JsonSerializerOptions"/> configured for safe, strict Polar API wire format.
    /// </summary>
    /// <value>
    /// Options with <c>MaxDepth=32</c>, camelCase naming, null-omission, and strict JSON compliance.
    /// </value>
    public static JsonSerializerOptions SafeOptions { get; } = new JsonSerializerOptions
    {
        MaxDepth = 32,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
}
