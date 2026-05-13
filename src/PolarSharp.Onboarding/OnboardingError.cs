namespace PolarSharp.Onboarding;

/// <summary>
/// Typed error returned from onboarding failure paths. Categorizes recoverable failure modes
/// the host can surface to users without leaking PolarSharp internals.
/// </summary>
/// <remarks>
/// Returned inside <c>PolarSharp.Result&lt;TValue, TError&gt;</c> from
/// <see cref="IPolarOnboardingClient.OnboardProgrammaticallyAsync"/>. Unrecoverable failures
/// (network down, internal exceptions in our own code) still throw — the Result type is for
/// expected-and-handleable outcomes only.
/// </remarks>
public sealed record OnboardingError
{
    /// <summary>Machine-readable error code — stable across versions.</summary>
    public required OnboardingErrorKind Kind { get; init; }

    /// <summary>Human-readable explanation safe to surface to end users.</summary>
    public required string Message { get; init; }

    /// <summary>The originating Polar API status code, if the failure came from a Polar HTTP response.</summary>
    public int? PolarStatusCode { get; init; }

    /// <summary>Field-level validation errors keyed by field name. Empty for non-validation failures.</summary>
    public IReadOnlyDictionary<string, string> FieldErrors { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Discriminator for the kind of onboarding failure.</summary>
public enum OnboardingErrorKind
{
    /// <summary>The Polar API rejected one or more fields (e.g. duplicate slug).</summary>
    PolarValidation,

    /// <summary>The configured OAuth client is missing or misconfigured.</summary>
    OAuthMisconfigured,

    /// <summary>The OAuth callback state did not match the value the host stored. Possible CSRF — abort.</summary>
    OAuthStateMismatch,

    /// <summary>The Polar token-exchange call failed (invalid code, expired, network error).</summary>
    OAuthTokenExchangeFailed,

    /// <summary>The organization was created but the webhook endpoint could not be registered. Partial state — see <see cref="OnboardingError.Message"/> for cleanup guidance.</summary>
    WebhookRegistrationFailed,

    /// <summary>The configured sink rejected the persistence of the result.</summary>
    SinkRejected,

    /// <summary>The request asked for a wizard session that doesn't exist, has expired, or has already been finalized.</summary>
    WizardSessionInvalid,

    /// <summary>A wizard step was submitted out of order — the wizard's <c>RemainingSteps</c> didn't include this step at the current position.</summary>
    WizardStepOutOfOrder,
}
