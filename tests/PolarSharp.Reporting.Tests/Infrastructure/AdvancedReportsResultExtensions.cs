using PolarSharp;

namespace PolarSharp.Reporting.Tests.Infrastructure;

/// <summary>Small test-only convenience: unwrap a <see cref="Result{TValue, TError}"/> assuming success.</summary>
internal static class AdvancedReportsResultExtensions
{
    /// <summary>Returns the success value, throwing xUnit if the result is a failure.</summary>
    public static TValue ValueOrThrow<TValue, TError>(this Result<TValue, TError> result) =>
        result.Match(
            onSuccess: v => v,
            onFailure: e => throw new Xunit.Sdk.XunitException($"Expected Success but got Failure: {e}"));
}
