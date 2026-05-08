using PolarSharp.Telemetry;

namespace PolarSharp.Logging;

/// <summary>
/// Builds log scope dictionaries for outbound Polar API calls, applying PII redaction
/// when <see cref="PolarLoggingOptions.RedactPii"/> is enabled.
/// </summary>
/// <remarks>
/// <para>
/// All callers that construct a log scope dictionary before calling
/// <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope{TState}(TState)"/> should go
/// through this builder so that PII masking is applied consistently and in a single place.
/// </para>
/// <para>
/// When <see cref="PolarLoggingOptions.RedactPii"/> is <see langword="true"/> (the default in
/// production), the raw dictionary is passed through <see cref="PolarPiiRedactor.Redact"/> before
/// being handed to the logger.  When it is <see langword="false"/> (development override) the raw
/// dictionary is returned as-is, giving developers access to unmasked values while debugging.
/// </para>
/// </remarks>
internal static class PolarScopeBuilder
{
    /// <summary>
    /// Returns a log scope dictionary ready for
    /// <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope{TState}(TState)"/>,
    /// with PII fields redacted when <see cref="PolarLoggingOptions.RedactPii"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="rawScope">
    /// The mutable scope dictionary whose values may contain PII.
    /// Ownership transfers to this method — do not read from it after calling <see cref="Build"/>.
    /// </param>
    /// <param name="loggingOptions">
    /// The logging sub-options for the current <see cref="PolarOptions"/> snapshot.
    /// </param>
    /// <returns>
    /// When <see cref="PolarLoggingOptions.RedactPii"/> is <see langword="true"/>,
    /// a new <see cref="IReadOnlyDictionary{TKey, TValue}"/> with PII values replaced.
    /// Otherwise, <paramref name="rawScope"/> cast to <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rawScope"/> or <paramref name="loggingOptions"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IReadOnlyDictionary<string, object?> Build(
        Dictionary<string, object?> rawScope,
        PolarLoggingOptions loggingOptions)
    {
        ArgumentNullException.ThrowIfNull(rawScope);
        ArgumentNullException.ThrowIfNull(loggingOptions);

        return loggingOptions.RedactPii
            ? PolarPiiRedactor.Redact(rawScope)
            : rawScope;
    }
}
