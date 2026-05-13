namespace PolarSharp.Onboarding.Wizard;

/// <summary>
/// EF Core entity backing wizard sessions. Holds the in-progress step state as a JSON blob
/// so adding new step types is non-breaking.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Not <c>ITenantOwned</c>.</strong> Onboarding precedes tenant creation — there is
/// no current tenant scope while a wizard session is in flight. Sessions live in the
/// shared tenant-store DbContext.
/// </para>
/// <para>
/// <strong>Encrypted-at-rest sensitive fields.</strong> The accumulated step data may
/// contain the tenant's translation API key — that field is encrypted via the ASP.NET Core
/// Data Protection API in <c>OnboardingWizard.SubmitTranslationConfigAsync</c> before being
/// serialized into <see cref="AccumulatedDataJson"/>. The plaintext is never persisted.
/// </para>
/// </remarks>
public sealed class OnboardingSessionEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC of the most recent step submission.</summary>
    public DateTimeOffset LastModifiedAt { get; set; }

    /// <summary>UTC after which the session is eligible for pruning.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>JSON array of completed <see cref="OnboardingStepKind"/> values, in order.</summary>
    public string CompletedStepsJson { get; set; } = "[]";

    /// <summary>JSON object containing the accumulated step submissions. Translation API keys inside this blob are encrypted.</summary>
    public string AccumulatedDataJson { get; set; } = "{}";

    /// <summary>True once <c>FinishAsync</c> has succeeded.</summary>
    public bool IsFinished { get; set; }

    /// <summary>Populated after a successful <c>FinishAsync</c> — the tenant id that was provisioned.</summary>
    public string? FinishedTenantId { get; set; }

    /// <summary>True when the host explicitly cancelled the session.</summary>
    public bool IsCanceled { get; set; }
}
