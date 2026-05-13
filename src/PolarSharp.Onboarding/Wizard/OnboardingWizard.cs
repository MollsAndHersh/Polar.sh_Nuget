using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.Onboarding.Wizard;

/// <summary>
/// Default <see cref="IOnboardingWizard"/> impl. Persists session state to the shared
/// tenant-store DbContext and encrypts in-flight sensitive fields (translation API keys)
/// via the ASP.NET Core Data Protection API.
/// </summary>
internal sealed class OnboardingWizard : IOnboardingWizard
{
    private const string ApiKeyProtectorPurpose = "PolarSharp.Onboarding.Wizard.TranslationApiKey";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PolarTenantDbContext _db;
    private readonly IPolarOnboardingClient _onboardingClient;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptions<PolarOnboardingOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<OnboardingWizard> _logger;

    public OnboardingWizard(
        PolarTenantDbContext db,
        IPolarOnboardingClient onboardingClient,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PolarOnboardingOptions> options,
        TimeProvider time,
        ILogger<OnboardingWizard> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(onboardingClient);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _onboardingClient = onboardingClient;
        _dataProtectionProvider = dataProtectionProvider;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<OnboardingSession> StartAsync(CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        var entity = new OnboardingSessionEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            LastModifiedAt = now,
            ExpiresAt = now.AddDays(_options.Value.Wizard.SessionTtlDays),
            CompletedStepsJson = "[]",
            AccumulatedDataJson = "{}",
        };
        _db.Set<OnboardingSessionEntity>().Add(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new OnboardingSession { Id = entity.Id, CreatedAt = entity.CreatedAt, ExpiresAt = entity.ExpiresAt };
    }

    public async Task<Result<OnboardingSessionSummary, OnboardingError>> GetCurrentStateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entity = await LoadActiveSessionAsync(sessionId, ct).ConfigureAwait(false);
        if (entity is null) return SessionInvalid();

        var completed = DeserializeCompleted(entity.CompletedStepsJson);
        var accumulated = DeserializeAccumulated(entity.AccumulatedDataJson);
        var remaining = ComputeRemainingSteps(completed, accumulated);

        return Result<OnboardingSessionSummary, OnboardingError>.Success(new OnboardingSessionSummary
        {
            Id = entity.Id,
            CurrentStep = completed.Count == 0 ? null : completed[^1],
            NextStep = remaining.Count == 0 ? null : remaining[0],
            CompletedSteps = completed,
            RemainingSteps = remaining,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
        });
    }

    public Task<Result<OnboardingStepResult, OnboardingError>> SubmitCompanyBasicsAsync(Guid sessionId, CompanyBasicsStep step, CancellationToken ct = default)
        => SubmitStepAsync(sessionId, OnboardingStepKind.CompanyBasics, step, ValidateCompanyBasics, ct);

    public Task<Result<OnboardingStepResult, OnboardingError>> SubmitProductTypesAsync(Guid sessionId, ProductTypesStep step, CancellationToken ct = default)
        => SubmitStepAsync(sessionId, OnboardingStepKind.ProductTypes, step, _ => [], ct);

    public Task<Result<OnboardingStepResult, OnboardingError>> SubmitWebhookConfigAsync(Guid sessionId, WebhookConfigStep step, CancellationToken ct = default)
        => SubmitStepAsync(sessionId, OnboardingStepKind.WebhookConfig, step, ValidateWebhookConfig, ct);

    public async Task<Result<OnboardingStepResult, OnboardingError>> SubmitTranslationConfigAsync(Guid sessionId, TranslationConfigStep step, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(step);

        // Encrypt the API key BEFORE persistence so the plaintext never touches disk or logs.
        TranslationConfigStep persistedStep = step;
        if (!string.IsNullOrEmpty(step.ApiKeyPlaintext))
        {
            var protector = _dataProtectionProvider.CreateProtector(ApiKeyProtectorPurpose);
            var encrypted = protector.Protect(step.ApiKeyPlaintext);
            persistedStep = step with { ApiKeyPlaintext = encrypted };
        }

        return await SubmitStepAsync(sessionId, OnboardingStepKind.TranslationConfig, persistedStep, _ => [], ct).ConfigureAwait(false);
    }

    public Task<Result<OnboardingStepResult, OnboardingError>> SubmitBankingHandoffAsync(Guid sessionId, BankingHandoffStep step, CancellationToken ct = default)
        => SubmitStepAsync(sessionId, OnboardingStepKind.BankingHandoff, step, _ => [], ct);

    public async Task<Result<OnboardedTenantResult, OnboardingError>> FinishAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entity = await LoadActiveSessionAsync(sessionId, ct).ConfigureAwait(false);
        if (entity is null)
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.WizardSessionInvalid,
                Message = "Session not found, expired, or already finalized.",
            });

        var completed = DeserializeCompleted(entity.CompletedStepsJson);
        var accumulated = DeserializeAccumulated(entity.AccumulatedDataJson);
        var remaining = ComputeRemainingSteps(completed, accumulated);
        if (remaining.Count > 0)
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.WizardStepOutOfOrder,
                Message = $"Wizard cannot finish — steps still pending: {string.Join(", ", remaining)}.",
            });

        if (!accumulated.TryGetValue(OnboardingStepKind.CompanyBasics, out var companyNode) ||
            !accumulated.TryGetValue(OnboardingStepKind.WebhookConfig, out var webhookNode))
        {
            return Result<OnboardedTenantResult, OnboardingError>.Failure(new OnboardingError
            {
                Kind = OnboardingErrorKind.WizardStepOutOfOrder,
                Message = "Required steps were not submitted.",
            });
        }

        var company = companyNode.Deserialize<CompanyBasicsStep>(JsonOptions)!;
        var webhook = webhookNode.Deserialize<WebhookConfigStep>(JsonOptions)!;

        var programmatic = new ProgrammaticOnboardingRequest
        {
            OrganizationName = company.OrganizationName,
            OrganizationSlug = company.OrganizationSlug,
            Email = company.Email,
            CountryCode = company.CountryCode,
            Currency = company.Currency,
            WebhookCallbackUrl = webhook.CallbackUrl,
            WebhookEvents = webhook.EventTypes,
            Server = _options.Value.Server,
            InitialAdminEmail = company.PrimaryAdminEmail,
        };

        var outcome = await _onboardingClient.OnboardProgrammaticallyAsync(programmatic, ct).ConfigureAwait(false);
        if (outcome.IsSuccess)
        {
            entity.IsFinished = true;
            entity.FinishedTenantId = outcome.Match<string?>(s => s.TenantId, _ => null);
            entity.LastModifiedAt = _time.GetUtcNow();
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return outcome;
    }

    public async Task CancelAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entity = await _db.Set<OnboardingSessionEntity>().FirstOrDefaultAsync(s => s.Id == sessionId, ct).ConfigureAwait(false);
        if (entity is null || entity.IsCanceled || entity.IsFinished) return;

        entity.IsCanceled = true;
        entity.LastModifiedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<OnboardingSessionEntity?> LoadActiveSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var entity = await _db.Set<OnboardingSessionEntity>().FirstOrDefaultAsync(s => s.Id == sessionId, ct).ConfigureAwait(false);
        if (entity is null) return null;
        if (entity.IsFinished || entity.IsCanceled) return null;
        if (entity.ExpiresAt <= _time.GetUtcNow()) return null;
        return entity;
    }

    private async Task<Result<OnboardingStepResult, OnboardingError>> SubmitStepAsync<TStep>(
        Guid sessionId,
        OnboardingStepKind kind,
        TStep step,
        Func<TStep, IReadOnlyList<string>> validate,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(step);

        var entity = await LoadActiveSessionAsync(sessionId, ct).ConfigureAwait(false);
        if (entity is null) return SessionInvalidStep();

        var validationErrors = validate(step);
        if (validationErrors.Count > 0)
        {
            return Result<OnboardingStepResult, OnboardingError>.Success(new OnboardingStepResult
            {
                CurrentStep = kind,
                NextStep = kind,
                IsValid = false,
                ValidationErrors = validationErrors,
                RemainingSteps = ComputeRemainingSteps(
                    DeserializeCompleted(entity.CompletedStepsJson),
                    DeserializeAccumulated(entity.AccumulatedDataJson)),
            });
        }

        var completed = DeserializeCompleted(entity.CompletedStepsJson).ToList();
        if (!completed.Contains(kind)) completed.Add(kind);

        var accumulated = DeserializeAccumulated(entity.AccumulatedDataJson);
        accumulated[kind] = JsonSerializer.SerializeToElement(step, JsonOptions);

        entity.CompletedStepsJson = JsonSerializer.Serialize(completed, JsonOptions);
        entity.AccumulatedDataJson = JsonSerializer.Serialize(accumulated, JsonOptions);
        entity.LastModifiedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        var remaining = ComputeRemainingSteps(completed, accumulated);
        return Result<OnboardingStepResult, OnboardingError>.Success(new OnboardingStepResult
        {
            CurrentStep = kind,
            NextStep = remaining.Count == 0 ? null : remaining[0],
            IsValid = true,
            ValidationErrors = [],
            RemainingSteps = remaining,
        });
    }

    private IReadOnlyList<OnboardingStepKind> ComputeRemainingSteps(
        IReadOnlyList<OnboardingStepKind> completed,
        IReadOnlyDictionary<OnboardingStepKind, JsonElement> accumulated)
    {
        // Determine whether the translation step applies based on the ProductTypes answers.
        bool needsTranslation = _options.Value.Wizard.RequireTranslationStepAtOnboarding;
        if (!needsTranslation && accumulated.TryGetValue(OnboardingStepKind.ProductTypes, out var productNode))
        {
            var product = productNode.Deserialize<ProductTypesStep>(JsonOptions);
            needsTranslation = product?.RequiresMultiLanguage ?? false;
        }

        var required = new List<OnboardingStepKind>
        {
            OnboardingStepKind.CompanyBasics,
            OnboardingStepKind.ProductTypes,
            OnboardingStepKind.WebhookConfig,
        };
        if (needsTranslation) required.Add(OnboardingStepKind.TranslationConfig);
        required.Add(OnboardingStepKind.BankingHandoff);

        return [.. required.Where(s => !completed.Contains(s))];
    }

    private static List<OnboardingStepKind> DeserializeCompleted(string json)
        => JsonSerializer.Deserialize<List<OnboardingStepKind>>(json, JsonOptions) ?? [];

    private static Dictionary<OnboardingStepKind, JsonElement> DeserializeAccumulated(string json)
        => JsonSerializer.Deserialize<Dictionary<OnboardingStepKind, JsonElement>>(json, JsonOptions) ?? [];

    private static IReadOnlyList<string> ValidateCompanyBasics(CompanyBasicsStep step)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(step.OrganizationName)) errors.Add("OrganizationName is required.");
        if (string.IsNullOrWhiteSpace(step.OrganizationSlug)) errors.Add("OrganizationSlug is required.");
        if (string.IsNullOrWhiteSpace(step.Email) || !step.Email.Contains('@')) errors.Add("Email must be a valid address.");
        if (step.CountryCode is null || step.CountryCode.Length != 2) errors.Add("CountryCode must be an ISO 3166-1 alpha-2 code.");
        if (step.Currency is null || step.Currency.Length != 3) errors.Add("Currency must be an ISO 4217 code (3 letters).");
        if (string.IsNullOrWhiteSpace(step.PrimaryAdminEmail) || !step.PrimaryAdminEmail.Contains('@')) errors.Add("PrimaryAdminEmail must be a valid address.");
        return errors;
    }

    private static IReadOnlyList<string> ValidateWebhookConfig(WebhookConfigStep step)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(step.CallbackUrl) ||
            !Uri.TryCreate(step.CallbackUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            errors.Add("CallbackUrl must be an absolute HTTPS URL.");
        if (step.EventTypes is null || step.EventTypes.Count == 0)
            errors.Add("At least one event type must be selected.");
        return errors;
    }

    private static Result<OnboardingSessionSummary, OnboardingError> SessionInvalid() =>
        Result<OnboardingSessionSummary, OnboardingError>.Failure(new OnboardingError
        {
            Kind = OnboardingErrorKind.WizardSessionInvalid,
            Message = "Session not found, expired, or already finalized.",
        });

    private static Result<OnboardingStepResult, OnboardingError> SessionInvalidStep() =>
        Result<OnboardingStepResult, OnboardingError>.Failure(new OnboardingError
        {
            Kind = OnboardingErrorKind.WizardSessionInvalid,
            Message = "Session not found, expired, or already finalized.",
        });
}
