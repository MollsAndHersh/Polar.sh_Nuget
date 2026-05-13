using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp;

namespace PolarSharp.Onboarding;

/// <summary>
/// Default <see cref="IPolarOnboardingClient"/> orchestrator.
/// </summary>
/// <remarks>
/// Composes <see cref="IPolarOnboardingApi"/> (the Polar HTTP surface),
/// <see cref="IOnboardedTenantSink"/> (persistence), and a chain of
/// <see cref="IOnboardingPostProcessor"/>s. The orchestration is the same regardless of
/// programmatic vs OAuth entry point — both converge after token acquisition into the same
/// "register webhook, persist, post-process" tail.
/// </remarks>
internal sealed class PolarOnboardingClient : IPolarOnboardingClient
{
    private readonly IPolarOnboardingApi _api;
    private readonly IOnboardedTenantSink _sink;
    private readonly IEnumerable<IOnboardingPostProcessor> _postProcessors;
    private readonly IOptions<PolarOnboardingOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<PolarOnboardingClient> _logger;

    public PolarOnboardingClient(
        IPolarOnboardingApi api,
        IOnboardedTenantSink sink,
        IEnumerable<IOnboardingPostProcessor> postProcessors,
        IOptions<PolarOnboardingOptions> options,
        TimeProvider time,
        ILogger<PolarOnboardingClient> logger)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(postProcessors);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _api = api;
        _sink = sink;
        _postProcessors = postProcessors;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<Result<OnboardedTenantResult, OnboardingError>> OnboardProgrammaticallyAsync(
        ProgrammaticOnboardingRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scopes = request.Scopes ?? _options.Value.DefaultScopes;

        var org = await _api.CreateOrganizationAsync(request, ct).ConfigureAwait(false);
        var oat = await _api.CreateOrganizationAccessTokenAsync(org.Id, scopes, ct).ConfigureAwait(false);

        PolarWebhookEndpointCreated webhook;
        try
        {
            webhook = await _api.CreateWebhookEndpointAsync(org.Id, request.WebhookCallbackUrl, request.WebhookEvents, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Webhook endpoint registration failed for organization {OrgId} after OAT was minted. " +
                "Organization and OAT exist in Polar but no webhook is bound — the host should either retry webhook registration or archive the organization in Polar.",
                org.Id);
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.WebhookRegistrationFailed,
                Message = $"Webhook registration failed: {ex.Message}",
            });
        }

        var result = new OnboardedTenantResult
        {
            TenantId = org.Id,
            OrganizationId = org.Id,
            OrganizationSlug = org.Slug,
            AccessToken = oat.Token,
            WebhookEndpointId = webhook.Id,
            WebhookSecret = webhook.Secret,
            Server = request.Server,
            GrantedScopes = oat.Scopes,
            OnboardedAt = _time.GetUtcNow(),
            InitialAdminEmail = request.InitialAdminEmail,
        };

        return await FinalizeAsync(result, ct).ConfigureAwait(false);
    }

    public OAuthAuthorizeUrl BuildAuthorizeUrl(OAuthOnboardingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.ClientId);
        ArgumentException.ThrowIfNullOrEmpty(request.RedirectUri);
        ArgumentException.ThrowIfNullOrEmpty(request.State);

        var baseUrl = request.Server == PolarServer.Sandbox
            ? "https://sandbox-api.polar.sh"
            : "https://api.polar.sh";

        var query = new[]
        {
            $"response_type=code",
            $"client_id={Uri.EscapeDataString(request.ClientId)}",
            $"redirect_uri={Uri.EscapeDataString(request.RedirectUri)}",
            $"scope={Uri.EscapeDataString(string.Join(' ', request.Scopes))}",
            $"state={Uri.EscapeDataString(request.State)}",
        };

        return new OAuthAuthorizeUrl($"{baseUrl}/v1/oauth2/authorize?{string.Join('&', query)}");
    }

    public async Task<Result<OnboardedTenantResult, OnboardingError>> CompleteOAuthOnboardingAsync(
        OAuthCallback callback,
        string webhookCallbackUrl,
        IReadOnlyList<string> webhookEvents,
        string? initialAdminEmail = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentException.ThrowIfNullOrEmpty(webhookCallbackUrl);
        ArgumentNullException.ThrowIfNull(webhookEvents);

        var oauth = _options.Value.OAuth;
        if (string.IsNullOrEmpty(oauth.ClientId) || string.IsNullOrEmpty(oauth.ClientSecret) || string.IsNullOrEmpty(oauth.RedirectUri))
        {
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.OAuthMisconfigured,
                Message = "OAuth client is not fully configured. Set PolarSharp:Onboarding:OAuth:ClientId / :ClientSecret / :RedirectUri.",
            });
        }

        PolarOAuthTokenResponse tokens;
        try
        {
            tokens = await _api.ExchangeOAuthCodeAsync(callback.Code, oauth.ClientId, oauth.ClientSecret, oauth.RedirectUri, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.OAuthTokenExchangeFailed,
                Message = $"Token exchange failed: {ex.Message}",
            });
        }

        PolarWebhookEndpointCreated webhook;
        try
        {
            webhook = await _api.CreateWebhookEndpointAsync(tokens.OrganizationId, webhookCallbackUrl, webhookEvents, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.WebhookRegistrationFailed,
                Message = $"Webhook registration failed: {ex.Message}",
            });
        }

        var result = new OnboardedTenantResult
        {
            TenantId = tokens.OrganizationId,
            OrganizationId = tokens.OrganizationId,
            OrganizationSlug = tokens.OrganizationId, // slug not present in token response — caller can update later
            AccessToken = tokens.AccessToken,
            WebhookEndpointId = webhook.Id,
            WebhookSecret = webhook.Secret,
            Server = _options.Value.Server,
            GrantedScopes = tokens.GrantedScopes,
            OnboardedAt = _time.GetUtcNow(),
            InitialAdminEmail = initialAdminEmail,
        };

        return await FinalizeAsync(result, ct).ConfigureAwait(false);
    }

    private async Task<Result<OnboardedTenantResult, OnboardingError>> FinalizeAsync(OnboardedTenantResult result, CancellationToken ct)
    {
        try
        {
            await _sink.PersistAsync(result, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Onboarded-tenant sink rejected the persistence of tenant {TenantId}", result.TenantId);
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.SinkRejected,
                Message = $"Sink rejected persistence: {ex.Message}",
            });
        }

        foreach (var processor in _postProcessors)
        {
            try
            {
                await processor.ProcessAsync(result, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Onboarding post-processor {Processor} threw for tenant {TenantId}. Subsequent processors are skipped; the tenant IS persisted.",
                    processor.GetType().Name, result.TenantId);
                break;
            }
        }

        _logger.LogInformation(
            "Onboarded tenant {TenantId} (org {OrgId}, slug {Slug}) against {Server}",
            result.TenantId, result.OrganizationId, result.OrganizationSlug, result.Server);

        return Result<OnboardedTenantResult, OnboardingError>.Success(result);
    }
}
