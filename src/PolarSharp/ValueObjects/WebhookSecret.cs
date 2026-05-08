namespace PolarSharp.ValueObjects;

/// <summary>
/// Represents a Polar webhook signing secret.
/// </summary>
/// <remarks>
/// <para>
/// Wraps the raw secret string to eliminate primitive obsession and prevent accidental
/// exposure in logs or debug output. <see cref="ToString"/> returns a masked representation
/// (<c>whsec_***</c>) regardless of the underlying value.
/// </para>
/// <para>
/// Accepts secrets with or without the <c>whsec_</c> prefix — the raw Base64 payload
/// is available via <see cref="Value"/> for HMAC computation.
/// </para>
/// </remarks>
public readonly record struct WebhookSecret
{
    /// <summary>Gets the raw secret value, including any <c>whsec_</c> prefix.</summary>
    /// <value>
    /// The original secret string as provided. Contains the <c>whsec_</c> prefix if the
    /// caller supplied it. Use this for storage and configuration round-trips.
    /// </value>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="WebhookSecret"/> from the given raw secret string.
    /// </summary>
    /// <param name="value">
    /// The signing secret. May include a <c>whsec_</c> prefix. Must not be empty or whitespace.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/> or whitespace.
    /// </exception>
    public WebhookSecret(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>
    /// Returns a masked string that prevents accidental secret exposure in logs,
    /// debug output, or exception messages.
    /// </summary>
    /// <returns>The literal string <c>whsec_***</c> — never the actual secret value.</returns>
    public override string ToString() => "whsec_***";

    /// <summary>Creates a <see cref="WebhookSecret"/> from the given raw string.</summary>
    /// <param name="value">
    /// The signing secret. May include a <c>whsec_</c> prefix. Must not be empty or whitespace.
    /// </param>
    /// <returns>A new <see cref="WebhookSecret"/> wrapping <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/> or whitespace.
    /// </exception>
    public static WebhookSecret From(string value) => new(value);
}
