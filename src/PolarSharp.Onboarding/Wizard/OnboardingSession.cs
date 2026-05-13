namespace PolarSharp.Onboarding.Wizard;

/// <summary>Pointer to a freshly created or resumed wizard session.</summary>
public sealed record OnboardingSession
{
    /// <summary>The session identifier — passed to subsequent step submissions.</summary>
    public required Guid Id { get; init; }
    /// <summary>UTC creation timestamp.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>UTC expiration timestamp — the session is auto-pruned after this.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>The full state snapshot of a wizard session — useful for resume + UI display.</summary>
public sealed record OnboardingSessionSummary
{
    /// <summary>The session identifier.</summary>
    public required Guid Id { get; init; }
    /// <summary>The most-recently-completed step. <see langword="null"/> when the session has just been started.</summary>
    public OnboardingStepKind? CurrentStep { get; init; }
    /// <summary>The next step the wizard expects, or <see langword="null"/> when ready to call <see cref="IOnboardingWizard.FinishAsync"/>.</summary>
    public OnboardingStepKind? NextStep { get; init; }
    /// <summary>Steps already submitted, in order.</summary>
    public required IReadOnlyList<OnboardingStepKind> CompletedSteps { get; init; }
    /// <summary>Steps still pending. Adjusts dynamically based on prior answers (e.g. <c>TranslationConfig</c> is removed when <c>ProductTypes.RequiresMultiLanguage</c> is false).</summary>
    public required IReadOnlyList<OnboardingStepKind> RemainingSteps { get; init; }
    /// <summary>True when all required steps have been completed and the caller may invoke <c>FinishAsync</c>.</summary>
    public bool IsReadyToFinish => RemainingSteps.Count == 0;
    /// <summary>UTC creation timestamp.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>UTC expiration timestamp.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>The outcome of submitting a single wizard step.</summary>
public sealed record OnboardingStepResult
{
    /// <summary>The step that was just submitted.</summary>
    public required OnboardingStepKind CurrentStep { get; init; }
    /// <summary>The next step the wizard expects, or <see langword="null"/> when ready to call <see cref="IOnboardingWizard.FinishAsync"/>.</summary>
    public OnboardingStepKind? NextStep { get; init; }
    /// <summary>Whether the submission was accepted.</summary>
    public required bool IsValid { get; init; }
    /// <summary>Validation errors. Empty when <see cref="IsValid"/> is <see langword="true"/>.</summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    /// <summary>Steps still pending after this submission.</summary>
    public required IReadOnlyList<OnboardingStepKind> RemainingSteps { get; init; }
}
