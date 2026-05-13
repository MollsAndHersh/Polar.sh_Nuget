namespace PolarSharp.Onboarding;

/// <summary>
/// Thin abstraction over the three Polar HTTP calls that compose programmatic onboarding.
/// Lets <see cref="IPolarOnboardingClient"/> orchestrate without depending directly on the
/// Kiota-generated client surface — and lets tests substitute a fake without standing up
/// real HTTP traffic to Polar.
/// </summary>
/// <remarks>
/// Default implementation in <c>KiotaPolarOnboardingApi</c> delegates to
/// <see cref="PolarClient"/>'s typed resource clients (Organizations,
/// OrganizationAccessTokens, Webhooks). Hosts can substitute their own implementation when
/// they need custom HTTP routing, request signing, or instrumentation.
/// </remarks>
public interface IPolarOnboardingApi
{
    /// <summary>POST <c>/v1/organizations/</c>. Returns the newly-created organization's id and slug.</summary>
    Task<PolarOrganizationCreated> CreateOrganizationAsync(
        ProgrammaticOnboardingRequest request,
        CancellationToken ct = default);

    /// <summary>POST <c>/v1/organization-access-tokens/</c>. Returns the OAT (the token value is readable ONLY in this response).</summary>
    Task<PolarOrganizationAccessTokenCreated> CreateOrganizationAccessTokenAsync(
        string organizationId,
        IReadOnlyList<string> scopes,
        CancellationToken ct = default);

    /// <summary>POST <c>/v1/webhooks/endpoints/</c>. Returns the endpoint id and shared signing secret.</summary>
    Task<PolarWebhookEndpointCreated> CreateWebhookEndpointAsync(
        string organizationId,
        string callbackUrl,
        IReadOnlyList<string> events,
        CancellationToken ct = default);

    /// <summary>POST <c>/v1/oauth2/token/</c> (form-urlencoded, grant_type=authorization_code). Returns the issued access + refresh tokens and the resolved organization id.</summary>
    Task<PolarOAuthTokenResponse> ExchangeOAuthCodeAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken ct = default);
}

/// <summary>Returned by <see cref="IPolarOnboardingApi.CreateOrganizationAsync"/>.</summary>
public sealed record PolarOrganizationCreated(string Id, string Slug);

/// <summary>Returned by <see cref="IPolarOnboardingApi.CreateOrganizationAccessTokenAsync"/>. The <see cref="Token"/> string is readable ONLY here — capture it immediately.</summary>
public sealed record PolarOrganizationAccessTokenCreated(string Id, string Token, IReadOnlyList<string> Scopes);

/// <summary>Returned by <see cref="IPolarOnboardingApi.CreateWebhookEndpointAsync"/>.</summary>
public sealed record PolarWebhookEndpointCreated(string Id, string Secret);

/// <summary>Returned by <see cref="IPolarOnboardingApi.ExchangeOAuthCodeAsync"/>.</summary>
public sealed record PolarOAuthTokenResponse(string AccessToken, string? RefreshToken, string OrganizationId, IReadOnlyList<string> GrantedScopes);
