using PolarSharp;

namespace PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;

/// <summary>
/// Test-only conveniences for asserting on <see cref="Result{TValue, TError}"/>. Production
/// code uses <c>Match</c>; tests need to assert on a specific branch and inspect the value
/// or error, which is awkward with <c>Match</c> alone.
/// </summary>
internal static class ResultTestExtensions
{
    /// <summary>Returns the success value, or throws an <c>Xunit</c> assertion failure with the error message when the result is a failure.</summary>
    public static TValue ValueOrThrow<TValue, TError>(this Result<TValue, TError> result) =>
        result.Match(
            onSuccess: v => v,
            onFailure: e => throw new Xunit.Sdk.XunitException($"Expected Success but got Failure: {e}"));

    /// <summary>Returns the error, or throws an <c>Xunit</c> assertion failure with the success value when the result is a success.</summary>
    public static TError ErrorOrThrow<TValue, TError>(this Result<TValue, TError> result) =>
        result.Match(
            onSuccess: v => throw new Xunit.Sdk.XunitException($"Expected Failure but got Success: {v}"),
            onFailure: e => e);
}
