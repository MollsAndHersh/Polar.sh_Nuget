using PolarSharp;

namespace PolarSharp.Onboarding;

/// <summary>
/// The top-level orchestrator for both onboarding flows — programmatic (single-call) and
/// OAuth-linking (authorize-URL + callback round-trip).
/// </summary>
/// <remarks>
/// Both flows produce the same <see cref="OnboardedTenantResult"/> shape on success. The
/// host typically:
/// <list type="number">
///   <item><description>Calls one of the methods on this interface.</description></item>
///   <item><description>On success, persists the result via <see cref="IOnboardedTenantSink"/> — usually <c>EfMultiTenantStoreSink</c> when the EF tenant store is installed.</description></item>
///   <item><description>Receives a TenantAdmin invitation email (via post-processors) if PolarSharp.MultiTenant.Identity is installed.</description></item>
/// </list>
/// </remarks>
public interface IPolarOnboardingClient
{
    /// <summary>
    /// Headless / B2B onboarding — provisions a Polar org, mints an OAT, registers the
    /// webhook endpoint, persists the result via the sink, and invokes all registered
    /// post-processors. Returns the populated <see cref="OnboardedTenantResult"/> on success.
    /// </summary>
    Task<Result<OnboardedTenantResult, OnboardingError>> OnboardProgrammaticallyAsync(
        ProgrammaticOnboardingRequest request,
        CancellationToken ct = default);

    /// <summary>Builds the URL the host redirects the user to so they can authorize PolarSharp's OAuth client.</summary>
    OAuthAuthorizeUrl BuildAuthorizeUrl(OAuthOnboardingRequest request);

    /// <summary>
    /// Completes the OAuth flow by exchanging the callback's authorization code for tokens,
    /// then registers the webhook endpoint, persists the result, and runs post-processors.
    /// </summary>
    /// <param name="callback">The values Polar returned on the redirect.</param>
    /// <param name="webhookCallbackUrl">The HTTPS URL Polar should deliver webhooks to.</param>
    /// <param name="webhookEvents">Event types the webhook subscribes to.</param>
    /// <param name="initialAdminEmail">Optional — email of the user to provision as TenantAdmin.</param>
    /// <param name="ct">Cancellation.</param>
    Task<Result<OnboardedTenantResult, OnboardingError>> CompleteOAuthOnboardingAsync(
        OAuthCallback callback,
        string webhookCallbackUrl,
        IReadOnlyList<string> webhookEvents,
        string? initialAdminEmail = null,
        CancellationToken ct = default);
}
