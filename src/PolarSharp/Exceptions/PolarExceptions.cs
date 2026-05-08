namespace PolarSharp;

/// <summary>
/// Base class for PolarSharp infrastructure exceptions. These exceptions represent
/// unrecoverable failures that cannot be expressed as typed <see cref="PolarError"/> values.
/// </summary>
/// <remarks>
/// Only three exception types cross the public boundary:
/// <list type="bullet">
///   <item><see cref="PolarNetworkException"/> — DNS, connection refused, TLS failure after all retries.</item>
///   <item><see cref="PolarConfigurationException"/> — invalid configuration detected at startup.</item>
///   <item><see cref="PolarWebhookConfigurationException"/> — webhooks registered without a secret.</item>
/// </list>
/// HTTP 4xx responses are never thrown — they surface as <see cref="PolarError"/> failure values.
/// </remarks>
public abstract class PolarException : Exception
{
    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">A human-readable description of the error.</param>
    protected PolarException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    protected PolarException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when the Polar API is unreachable after all retry attempts — DNS failure,
/// connection refused, or TLS error. This is genuinely unrecoverable at the call site;
/// the host application's global exception handler should deal with it.
/// </summary>
public sealed class PolarNetworkException : PolarException
{
    /// <summary>
    /// Initializes a new instance describing a network-level failure reaching Polar.
    /// </summary>
    /// <param name="message">A human-readable description of the network failure.</param>
    /// <param name="innerException">The underlying <see cref="HttpRequestException"/> or socket exception.</param>
    public PolarNetworkException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown at application startup when PolarSharp configuration is invalid.
/// Invalid configuration fails startup via <c>ValidateOnStart()</c> before the app serves any traffic.
/// </summary>
public sealed class PolarConfigurationException : PolarException
{
    /// <summary>
    /// Initializes a new instance describing a configuration validation failure.
    /// </summary>
    /// <param name="message">A human-readable description of what is misconfigured.</param>
    public PolarConfigurationException(string message) : base(message) { }
}

/// <summary>
/// Thrown at application startup when <c>AddPolarWebhooks()</c> is registered but
/// <c>PolarSharp:Webhooks:Secret</c> (or <c>Secrets</c> list) is empty.
/// </summary>
/// <remarks>
/// Startup-time only — not thrown on any request path. Prevents silent HMAC bypass
/// at runtime due to misconfiguration.
/// </remarks>
public sealed class PolarWebhookConfigurationException : PolarException
{
    /// <summary>
    /// Initializes a new instance describing a webhook configuration failure.
    /// </summary>
    /// <param name="message">A human-readable description of the webhook configuration issue.</param>
    public PolarWebhookConfigurationException(string message) : base(message) { }
}
