using PolarSharp;

namespace PolarSharp.Onboarding.Wizard;

/// <summary>
/// Step-by-step wizard for interactive UI flows. Internally accumulates state in a
/// persistent <see cref="OnboardingSessionEntity"/> row, then calls
/// <see cref="IPolarOnboardingClient.OnboardProgrammaticallyAsync"/> at
/// <see cref="FinishAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Sessions are resumable: a partially-completed wizard survives browser refreshes / app
/// restarts. Sessions expire after <see cref="PolarOnboardingOptions.WizardOptions.SessionTtlDays"/>
/// (default 7 days) and the <see cref="OnboardingSessionExpirationCleaner"/> hosted service
/// prunes expired rows daily.
/// </para>
/// <para>
/// <strong>Conditional next-steps:</strong> <see cref="OnboardingStepResult.NextStep"/> is
/// computed from the answers accumulated so far. For example, when
/// <see cref="ProductTypesStep.RequiresMultiLanguage"/> is <see langword="false"/>, the
/// <see cref="OnboardingStepKind.TranslationConfig"/> step is removed from
/// <see cref="OnboardingStepResult.RemainingSteps"/>.
/// </para>
/// </remarks>
public interface IOnboardingWizard
{
    /// <summary>Creates a new wizard session. Returns the session pointer + expiration.</summary>
    Task<OnboardingSession> StartAsync(CancellationToken ct = default);

    /// <summary>Retrieves the current state of an in-progress session. Returns a failure result when the session doesn't exist, has expired, or has already been finalized.</summary>
    Task<Result<OnboardingSessionSummary, OnboardingError>> GetCurrentStateAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Submits the <see cref="OnboardingStepKind.CompanyBasics"/> step.</summary>
    Task<Result<OnboardingStepResult, OnboardingError>> SubmitCompanyBasicsAsync(Guid sessionId, CompanyBasicsStep step, CancellationToken ct = default);

    /// <summary>Submits the <see cref="OnboardingStepKind.ProductTypes"/> step. Answers here drive whether the <see cref="OnboardingStepKind.TranslationConfig"/> step appears next.</summary>
    Task<Result<OnboardingStepResult, OnboardingError>> SubmitProductTypesAsync(Guid sessionId, ProductTypesStep step, CancellationToken ct = default);

    /// <summary>Submits the <see cref="OnboardingStepKind.WebhookConfig"/> step.</summary>
    Task<Result<OnboardingStepResult, OnboardingError>> SubmitWebhookConfigAsync(Guid sessionId, WebhookConfigStep step, CancellationToken ct = default);

    /// <summary>Submits the optional <see cref="OnboardingStepKind.TranslationConfig"/> step. Encrypts the supplied API key at rest immediately.</summary>
    Task<Result<OnboardingStepResult, OnboardingError>> SubmitTranslationConfigAsync(Guid sessionId, TranslationConfigStep step, CancellationToken ct = default);

    /// <summary>Submits the final <see cref="OnboardingStepKind.BankingHandoff"/> step.</summary>
    Task<Result<OnboardingStepResult, OnboardingError>> SubmitBankingHandoffAsync(Guid sessionId, BankingHandoffStep step, CancellationToken ct = default);

    /// <summary>Commits the wizard — internally invokes the programmatic onboarding path. Returns the same <see cref="OnboardedTenantResult"/> the headless API returns.</summary>
    Task<Result<OnboardedTenantResult, OnboardingError>> FinishAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Explicitly cancels the session. Idempotent — cancelling a missing or already-cancelled session is a no-op.</summary>
    Task CancelAsync(Guid sessionId, CancellationToken ct = default);
}
