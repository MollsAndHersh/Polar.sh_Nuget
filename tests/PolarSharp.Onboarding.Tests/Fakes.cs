using PolarSharp.Onboarding;

namespace PolarSharp.Onboarding.Tests;

/// <summary>
/// In-memory fake <see cref="IPolarOnboardingApi"/> — records every call and returns
/// pre-configured fake responses. Use to exercise the orchestrator without HTTP traffic.
/// </summary>
internal sealed class FakePolarOnboardingApi : IPolarOnboardingApi
{
    public List<ProgrammaticOnboardingRequest> CreateOrgCalls { get; } = [];
    public List<(string OrgId, IReadOnlyList<string> Scopes)> CreateOatCalls { get; } = [];
    public List<(string OrgId, string Url, IReadOnlyList<string> Events)> CreateWebhookCalls { get; } = [];
    public List<(string Code, string ClientId, string Secret, string RedirectUri)> ExchangeCalls { get; } = [];

    public PolarOrganizationCreated OrgResult { get; set; } = new("org_test_001", "test-slug");
    public PolarOrganizationAccessTokenCreated OatResult { get; set; } = new("oat_test_001", "polar_oat_test_token_value", ["products:write"]);
    public PolarWebhookEndpointCreated WebhookResult { get; set; } = new("whep_test_001", "whsec_test_secret");
    public PolarOAuthTokenResponse OAuthResult { get; set; } = new("polar_oauth_token_value", "polar_refresh_value", "org_test_oauth", ["products:write"]);

    public Func<Task>? OnWebhookCall { get; set; }

    public Task<PolarOrganizationCreated> CreateOrganizationAsync(ProgrammaticOnboardingRequest request, CancellationToken ct = default)
    {
        CreateOrgCalls.Add(request);
        return Task.FromResult(OrgResult);
    }

    public Task<PolarOrganizationAccessTokenCreated> CreateOrganizationAccessTokenAsync(string organizationId, IReadOnlyList<string> scopes, CancellationToken ct = default)
    {
        CreateOatCalls.Add((organizationId, scopes));
        return Task.FromResult(OatResult);
    }

    public async Task<PolarWebhookEndpointCreated> CreateWebhookEndpointAsync(string organizationId, string callbackUrl, IReadOnlyList<string> events, CancellationToken ct = default)
    {
        CreateWebhookCalls.Add((organizationId, callbackUrl, events));
        if (OnWebhookCall is not null) await OnWebhookCall().ConfigureAwait(false);
        return WebhookResult;
    }

    public Task<PolarOAuthTokenResponse> ExchangeOAuthCodeAsync(string code, string clientId, string clientSecret, string redirectUri, CancellationToken ct = default)
    {
        ExchangeCalls.Add((code, clientId, clientSecret, redirectUri));
        return Task.FromResult(OAuthResult);
    }
}

/// <summary>In-memory sink that records the persisted result.</summary>
internal sealed class RecordingSink : IOnboardedTenantSink
{
    public List<OnboardedTenantResult> Persisted { get; } = [];
    public Exception? ThrowOnNextPersist { get; set; }

    public Task PersistAsync(OnboardedTenantResult result, CancellationToken ct = default)
    {
        if (ThrowOnNextPersist is { } ex)
        {
            ThrowOnNextPersist = null;
            throw ex;
        }
        Persisted.Add(result);
        return Task.CompletedTask;
    }
}

/// <summary>Counts post-processor invocations for assertions.</summary>
internal sealed class CountingPostProcessor : IOnboardingPostProcessor
{
    public int CallCount { get; private set; }
    public OnboardedTenantResult? LastResult { get; private set; }

    public Task ProcessAsync(OnboardedTenantResult result, CancellationToken ct = default)
    {
        CallCount++;
        LastResult = result;
        return Task.CompletedTask;
    }
}
