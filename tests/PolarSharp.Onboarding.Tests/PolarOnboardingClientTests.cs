using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp;
using PolarSharp.Onboarding;

namespace PolarSharp.Onboarding.Tests;

/// <summary>
/// Verifies the orchestration logic: programmatic happy path, OAuth happy path, sink
/// failure handling, webhook failure handling, and post-processor invocation.
/// </summary>
public sealed class PolarOnboardingClientTests
{
    private static PolarOnboardingClient BuildClient(
        FakePolarOnboardingApi api,
        IOnboardedTenantSink sink,
        params IOnboardingPostProcessor[] processors)
    {
        var options = Options.Create(new PolarOnboardingOptions
        {
            OAuth = new PolarOnboardingOptions.OAuthOptions
            {
                ClientId = "polar_test_client_id",
                ClientSecret = "polar_test_client_secret",
                RedirectUri = "https://app.example.com/callback",
            },
        });
        return new PolarOnboardingClient(
            api, sink, processors, options, TimeProvider.System, NullLogger<PolarOnboardingClient>.Instance);
    }

    [Fact]
    public async Task Programmatic_happy_path_calls_three_polar_APIs_and_persists_via_sink()
    {
        var api = new FakePolarOnboardingApi();
        var sink = new RecordingSink();
        var processor = new CountingPostProcessor();
        var client = BuildClient(api, sink, processor);

        var result = await client.OnboardProgrammaticallyAsync(new ProgrammaticOnboardingRequest
        {
            OrganizationName = "Acme",
            OrganizationSlug = "acme",
            Email = "ops@acme.example.com",
            CountryCode = "US",
            Currency = "USD",
            WebhookCallbackUrl = "https://app.example.com/hooks/polar",
            WebhookEvents = ["order.created", "order.paid"],
            Server = PolarServer.Sandbox,
            InitialAdminEmail = "admin@acme.example.com",
        });

        Assert.True(result.IsSuccess);
        Assert.Single(api.CreateOrgCalls);
        Assert.Single(api.CreateOatCalls);
        Assert.Single(api.CreateWebhookCalls);
        Assert.Single(sink.Persisted);
        Assert.Equal(1, processor.CallCount);
        Assert.Equal("org_test_001", processor.LastResult!.OrganizationId);
        Assert.Equal("admin@acme.example.com", processor.LastResult.InitialAdminEmail);
    }

    [Fact]
    public async Task Webhook_failure_returns_typed_error_and_does_not_persist()
    {
        var api = new FakePolarOnboardingApi
        {
            OnWebhookCall = () => throw new InvalidOperationException("Polar HTTP 503"),
        };
        var sink = new RecordingSink();
        var processor = new CountingPostProcessor();
        var client = BuildClient(api, sink, processor);

        var result = await client.OnboardProgrammaticallyAsync(MinimalRequest());

        Assert.False(result.IsSuccess);
        var err = result.Match<OnboardingError?>(_ => null, e => e);
        Assert.NotNull(err);
        Assert.Equal(OnboardingErrorKind.WebhookRegistrationFailed, err.Kind);
        Assert.Contains("503", err.Message);
        Assert.Empty(sink.Persisted);
        Assert.Equal(0, processor.CallCount);
    }

    [Fact]
    public async Task Sink_failure_returns_SinkRejected_error_and_skips_post_processors()
    {
        var api = new FakePolarOnboardingApi();
        var sink = new RecordingSink { ThrowOnNextPersist = new InvalidOperationException("DB down") };
        var processor = new CountingPostProcessor();
        var client = BuildClient(api, sink, processor);

        var result = await client.OnboardProgrammaticallyAsync(MinimalRequest());

        Assert.False(result.IsSuccess);
        var err = result.Match<OnboardingError?>(_ => null, e => e);
        Assert.NotNull(err);
        Assert.Equal(OnboardingErrorKind.SinkRejected, err.Kind);
        Assert.Equal(0, processor.CallCount);
    }

    [Fact]
    public async Task OAuth_misconfigured_returns_OAuthMisconfigured_error()
    {
        var api = new FakePolarOnboardingApi();
        var sink = new RecordingSink();
        // Override options to MISSING client secret.
        var options = Options.Create(new PolarOnboardingOptions
        {
            OAuth = new PolarOnboardingOptions.OAuthOptions { ClientId = "x", ClientSecret = null, RedirectUri = "https://app/cb" },
        });
        var client = new PolarOnboardingClient(api, sink, [], options, TimeProvider.System, NullLogger<PolarOnboardingClient>.Instance);

        var result = await client.CompleteOAuthOnboardingAsync(
            new OAuthCallback { Code = "abc", State = "xyz" },
            "https://app.example.com/hooks/polar",
            ["order.created"]);

        Assert.False(result.IsSuccess);
        var err = result.Match<OnboardingError?>(_ => null, e => e);
        Assert.NotNull(err);
        Assert.Equal(OnboardingErrorKind.OAuthMisconfigured, err.Kind);
        Assert.Empty(api.ExchangeCalls);
    }

    [Fact]
    public async Task OAuth_happy_path_exchanges_code_then_registers_webhook()
    {
        var api = new FakePolarOnboardingApi();
        var sink = new RecordingSink();
        var client = BuildClient(api, sink);

        var result = await client.CompleteOAuthOnboardingAsync(
            new OAuthCallback { Code = "code_test", State = "state_test" },
            "https://app.example.com/hooks/polar",
            ["order.created"],
            initialAdminEmail: "admin@example.com");

        Assert.True(result.IsSuccess);
        Assert.Single(api.ExchangeCalls);
        Assert.Single(api.CreateWebhookCalls);
        Assert.Empty(api.CreateOrgCalls);   // OAuth path bypasses programmatic org creation
        Assert.Empty(api.CreateOatCalls);   // OAT comes from token exchange, not a separate call
        Assert.Equal("polar_oauth_token_value", sink.Persisted[0].AccessToken);
        Assert.Equal("admin@example.com", sink.Persisted[0].InitialAdminEmail);
    }

    [Fact]
    public void BuildAuthorizeUrl_produces_well_formed_polar_oauth_url()
    {
        var client = BuildClient(new FakePolarOnboardingApi(), new RecordingSink());

        var url = client.BuildAuthorizeUrl(new OAuthOnboardingRequest
        {
            ClientId = "client_test",
            RedirectUri = "https://app.example.com/cb",
            Scopes = ["products:write", "events:read"],
            State = "csrf_xyz",
            WebhookCallbackUrl = "https://app.example.com/hooks",
            WebhookEvents = ["order.created"],
            Server = PolarServer.Sandbox,
        });

        Assert.StartsWith("https://sandbox-api.polar.sh/v1/oauth2/authorize?", url.Url);
        Assert.Contains("client_id=client_test", url.Url);
        Assert.Contains("scope=products%3Awrite%20events%3Aread", url.Url);
        Assert.Contains("state=csrf_xyz", url.Url);
    }

    [Fact]
    public void BuildAuthorizeUrl_targets_production_when_requested()
    {
        var client = BuildClient(new FakePolarOnboardingApi(), new RecordingSink());

        var url = client.BuildAuthorizeUrl(new OAuthOnboardingRequest
        {
            ClientId = "c",
            RedirectUri = "https://app/cb",
            Scopes = ["x"],
            State = "s",
            WebhookCallbackUrl = "https://app/h",
            WebhookEvents = ["y"],
            Server = PolarServer.Production,
        });

        Assert.StartsWith("https://api.polar.sh/v1/oauth2/authorize", url.Url);
    }

    private static ProgrammaticOnboardingRequest MinimalRequest() => new()
    {
        OrganizationName = "X",
        OrganizationSlug = "x",
        Email = "ops@x.example.com",
        CountryCode = "US",
        Currency = "USD",
        WebhookCallbackUrl = "https://app.example.com/hooks",
        WebhookEvents = ["order.created"],
    };
}
