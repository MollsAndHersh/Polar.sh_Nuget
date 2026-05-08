namespace PolarSharp.Versioning;

/// <summary>
/// Provides compile-time metadata about the Polar.sh API version that the bundled Kiota client
/// was generated against.
/// </summary>
/// <remarks>
/// <see cref="GeneratedAgainstVersion"/> is a compile-time constant updated automatically by
/// the Kiota regeneration script each time the client is regenerated from a new OpenAPI spec.
/// Commit both the regenerated <c>Generated/</c> folder and this file together.
/// <para>
/// Accessible at runtime via <c>PolarClient.GeneratedAgainstVersion</c> for diagnostic output.
/// </para>
/// </remarks>
internal static class PolarApiMetadata
{
    /// <summary>
    /// The Polar.sh API version date the bundled Kiota client was generated against.
    /// Format: <c>YYYY-MM-DD</c>.
    /// </summary>
    /// <remarks>
    /// Updated automatically by the Kiota regeneration script.
    /// This constant drives startup mismatch detection when <see cref="PolarOptions.ApiVersion"/>
    /// is also set and <see cref="PolarOptions.ApiVersionStrictness"/> is not
    /// <see cref="PolarApiVersionStrictness.Off"/>.
    /// </remarks>
    public const string GeneratedAgainstVersion = "2025-01-15";

    /// <summary>The OpenAPI spec URL used at codegen time, preserved for traceability.</summary>
    public const string SpecSourceUrl = "https://api.polar.sh/openapi.json";
}
