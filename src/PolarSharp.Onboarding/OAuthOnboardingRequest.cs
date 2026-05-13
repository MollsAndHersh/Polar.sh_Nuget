namespace PolarSharp.Onboarding;

/// <summary>Request shape for building a Polar OAuth authorization URL.</summary>
public sealed record OAuthOnboardingRequest
{
    /// <summary>Polar OAuth client identifier.</summary>
    public required string ClientId { get; init; }

    /// <summary>Where Polar should redirect the user after consent. Must match a registered URI in the Polar OAuth client.</summary>
    public required string RedirectUri { get; init; }

    /// <summary>OAuth scopes to request.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>CSRF-protection state value — the host generates a random token, includes it here, and verifies it on callback.</summary>
    public required string State { get; init; }

    /// <summary>HTTPS URL the Polar webhook will deliver events to (set up AFTER successful token exchange).</summary>
    public required string WebhookCallbackUrl { get; init; }

    /// <summary>The Polar event types the webhook should subscribe to.</summary>
    public required IReadOnlyList<string> WebhookEvents { get; init; }

    /// <summary>The Polar environment to authorize against. Default: <see cref="PolarServer.Sandbox"/>.</summary>
    public PolarServer Server { get; init; } = PolarServer.Sandbox;
}

/// <summary>The data Polar sends back to the host's redirect URI after the user consents.</summary>
public sealed record OAuthCallback
{
    /// <summary>The single-use authorization code Polar issued.</summary>
    public required string Code { get; init; }

    /// <summary>The state value the host originally supplied — must be checked against the stored value to defeat CSRF.</summary>
    public required string State { get; init; }
}

/// <summary>The built authorization URL the host redirects the user to.</summary>
public sealed record OAuthAuthorizeUrl(string Url);
