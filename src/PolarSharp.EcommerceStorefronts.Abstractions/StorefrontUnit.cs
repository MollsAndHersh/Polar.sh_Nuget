namespace PolarSharp.EcommerceStorefronts.Abstractions;

/// <summary>
/// Singleton representing the "no return value" success case for void-style operations
/// returning <see cref="StorefrontResult{TValue}"/>.
/// </summary>
/// <remarks>
/// Lift-safe equivalent of MediatR's <c>Unit</c>. Use when an operation has no
/// meaningful success value but still needs to surface either success or
/// <see cref="StorefrontError"/>.
/// </remarks>
public readonly record struct StorefrontUnit
{
    /// <summary>The singleton instance.</summary>
    public static readonly StorefrontUnit Value = default;
}
