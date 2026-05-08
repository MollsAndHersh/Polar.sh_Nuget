namespace PolarSharp.Webhooks;

/// <summary>
/// Thrown at application startup when the webhook configuration is invalid or incomplete.
/// </summary>
/// <remarks>
/// <para>
/// This exception is a startup-only, non-request-path exception. It is thrown by
/// <see cref="PolarWebhookStartupValidator"/> when
/// <see cref="PolarWebhookOptions.FailOnMissingHandlers"/> is <see langword="true"/> and
/// one or more known Polar event types have no registered handler.
/// </para>
/// <para>
/// It is also thrown by the webhook options validator when <c>PolarSharp:Webhooks:Secret</c>
/// and <c>PolarSharp:Webhooks:Secrets</c> are both empty.
/// </para>
/// <para>
/// Never thrown during request processing — only during <c>IHost.StartAsync()</c>.
/// </para>
/// </remarks>
public sealed class PolarWebhookConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="PolarWebhookConfigurationException"/> with a descriptive message.
    /// </summary>
    /// <param name="message">
    /// A message describing the configuration problem, including the names of any
    /// unregistered event types and the corrective action.
    /// </param>
    public PolarWebhookConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="PolarWebhookConfigurationException"/> with a descriptive
    /// message and an inner exception.
    /// </summary>
    /// <param name="message">A message describing the configuration problem.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public PolarWebhookConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
