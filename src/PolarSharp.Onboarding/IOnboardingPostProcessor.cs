namespace PolarSharp.Onboarding;

/// <summary>
/// Extension point invoked after a successful onboarding flow — runs in registration order
/// AFTER the sink has persisted the result.
/// </summary>
/// <remarks>
/// <para>
/// Multiple implementations can register simultaneously. The orchestrator invokes them in
/// registration order; failures throw and abort downstream processors (the onboarded tenant
/// IS persisted at this point — abort means "skip remaining hooks", not "roll back").
/// </para>
/// <para>
/// PolarSharp.MultiTenant.Identity registers a post-processor that auto-provisions a
/// TenantAdmin user via M:N membership. Hosts can register additional processors for things
/// like welcome emails, analytics events, audit-log entries.
/// </para>
/// </remarks>
public interface IOnboardingPostProcessor
{
    /// <summary>Invoked after the sink has persisted the onboarded tenant.</summary>
    /// <param name="result">The fully populated result of the onboarding flow.</param>
    /// <param name="ct">Cancellation.</param>
    Task ProcessAsync(OnboardedTenantResult result, CancellationToken ct = default);
}
