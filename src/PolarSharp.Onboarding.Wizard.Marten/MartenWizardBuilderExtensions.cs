using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.Onboarding.Wizard.Marten;

/// <summary>
/// Marten-backed event-sourced onboarding-wizard session storage.
/// </summary>
/// <remarks>
/// <para>
/// An onboarding-wizard session is a sequence of step submissions over time — a natural
/// fit for an append-only event stream. Marten's projection support lets the wizard's
/// "current state" view be rebuilt from the event stream at any point, and snapshots make
/// resume-after-days-of-pause fast.
/// </para>
/// <para>
/// Phase 15 ships the registration + Postgres connection. The full <c>WizardStepSubmitted</c>
/// event hierarchy + <c>WizardSessionStateProjection</c> + the migration story from the EF
/// Core <c>onboarding_sessions</c> table is deferred to a Phase 15.x patch.
/// </para>
/// </remarks>
public static class MartenWizardBuilderExtensions
{
    /// <summary>
    /// Registers Marten as the event-sourced onboarding-wizard session storage backend.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string used for the Marten document store.</param>
    /// <param name="schemaName">Optional Postgres schema name (defaults to <c>"polar_marten_wizard"</c>).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseMartenOnboardingWizard(
        this IServiceCollection services,
        string postgresConnectionString,
        string schemaName = "polar_marten_wizard")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(postgresConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(schemaName);

        services.AddMarten(opts =>
        {
            opts.Connection(postgresConnectionString);
            opts.DatabaseSchemaName = schemaName;
            // Event-type registration + projection wiring in Phase 15.x.
        });

        return services;
    }
}
