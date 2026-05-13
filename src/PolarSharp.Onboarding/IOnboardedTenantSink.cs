namespace PolarSharp.Onboarding;

/// <summary>
/// Persists the result of a successful onboarding flow. Decouples
/// <see cref="IPolarOnboardingClient"/> from any specific tenant-store implementation.
/// </summary>
/// <remarks>
/// <para>
/// PolarSharp ships <c>EfMultiTenantStoreSink</c> which writes the result directly into the
/// EF Core tenant registry (and invalidates the tenant cache on success). Hosts with custom
/// tenant storage register their own implementation.
/// </para>
/// <para>
/// Failures inside <see cref="PersistAsync"/> should throw — the onboarding client wraps the
/// exception as <see cref="OnboardingErrorKind.SinkRejected"/> and surfaces it as a typed
/// failure to the caller.
/// </para>
/// </remarks>
public interface IOnboardedTenantSink
{
    /// <summary>Persists the supplied <paramref name="result"/>. Throw on persistence failure.</summary>
    Task PersistAsync(OnboardedTenantResult result, CancellationToken ct = default);
}

/// <summary>
/// No-op default — registered when no real sink is supplied. Lets tests and headless
/// scenarios run without forcing the host to wire a persistence backend.
/// </summary>
internal sealed class NoOpOnboardedTenantSink : IOnboardedTenantSink
{
    public Task PersistAsync(OnboardedTenantResult result, CancellationToken ct = default) => Task.CompletedTask;
}
