namespace PolarSharp;

/// <summary>
/// Internal singleton tracking which PolarSharp features were registered during
/// <c>AddPolarInfrastructure</c> and its optional extension calls.
/// </summary>
/// <remarks>
/// Consumed by <c>UsePolarInfrastructure</c> to conditionally activate middleware
/// only for features that were actually registered — enabling no-op safety when
/// only the core package is installed.
/// </remarks>
internal sealed class PolarInfrastructureMarker
{
    /// <summary>Gets or sets whether <c>AddPolarWebhooks()</c> was called.</summary>
    public bool WebhooksRegistered { get; set; }

    /// <summary>Gets or sets whether <c>AddPolarMultiTenant()</c> was called.</summary>
    public bool MultiTenantRegistered { get; set; }

    /// <summary>Gets or sets whether <c>AddPolarToastNotifications()</c> was called.</summary>
    public bool ToastNotificationsRegistered { get; set; }

    /// <summary>Gets or sets the webhook endpoint path, set when <see cref="WebhooksRegistered"/> is <see langword="true"/>.</summary>
    public string WebhookPath { get; set; } = "/hooks/polar";
}
