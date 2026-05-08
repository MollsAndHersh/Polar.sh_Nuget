namespace PolarSharp;

/// <summary>
/// Represents a Polar.sh access token (Organization Access Token or Customer Access Token).
/// Prevents raw string tokens from leaking into log output via <see cref="ToString"/>.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> returns a masked representation — never the raw token value —
/// so accidental logging (e.g., structured log serialization) never exposes credentials.
/// </remarks>
public readonly record struct PolarAccessToken
{
    private readonly string _value;

    private PolarAccessToken(string value) => _value = value;

    /// <summary>
    /// Creates a <see cref="PolarAccessToken"/> from a raw token string.
    /// </summary>
    /// <param name="value">The raw Polar access token. Must not be null or whitespace.</param>
    /// <returns>A validated <see cref="PolarAccessToken"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public static PolarAccessToken From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new PolarAccessToken(value);
    }

    /// <summary>Gets the raw token value for use in HTTP Authorization headers.</summary>
    /// <remarks>
    /// Use this property only in <see cref="Auth.BearerTokenHandler"/> where the raw value is required.
    /// Never pass this to any logger or diagnostic system.
    /// </remarks>
    public string RawValue => _value;

    /// <summary>
    /// Returns a masked representation of the token. Never returns the raw value.
    /// </summary>
    /// <returns>A string of the form <c>tok_***[last4]</c> that is safe for logging.</returns>
    public override string ToString()
    {
        if (string.IsNullOrEmpty(_value)) return "***";
        var suffix = _value.Length > 4 ? _value[^4..] : _value;
        return $"***{suffix}";
    }
}

///<summary>
/// Represents a Polar.sh tenant identifier in a multi-tenant deployment.
/// </summary>
public readonly record struct TenantId
{
    /// <summary>Gets the raw tenant identifier string.</summary>
    public string Value { get; }

    private TenantId(string value) => Value = value;

    /// <summary>
    /// Creates a <see cref="TenantId"/> from a raw identifier string.
    /// </summary>
    /// <param name="value">The tenant identifier. Must not be null or whitespace.</param>
    /// <returns>A validated <see cref="TenantId"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public static TenantId From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new TenantId(value);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Represents an idempotency key used to prevent duplicate operations on retried requests.
/// </summary>
public readonly record struct IdempotencyKey
{
    /// <summary>Gets the raw idempotency key string.</summary>
    public string Value { get; }

    private IdempotencyKey(string value) => Value = value;

    /// <summary>
    /// Creates a new random <see cref="IdempotencyKey"/> based on a new GUID.
    /// </summary>
    /// <returns>A unique idempotency key.</returns>
    public static IdempotencyKey NewKey() => new(Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Creates an <see cref="IdempotencyKey"/> from an existing string value.
    /// </summary>
    /// <param name="value">The idempotency key value. Must not be null or whitespace.</param>
    /// <returns>A validated <see cref="IdempotencyKey"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public static IdempotencyKey From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new IdempotencyKey(value);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
