using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity.Extensions;
using PolarSharp.Onboarding;

namespace PolarSharp.MultiTenant.Identity.Onboarding;

/// <summary>
/// Bridges PolarSharp Identity into the Onboarding package — registers
/// <see cref="TenantAdminAutoProvisioningPostProcessor"/> as an
/// <see cref="IOnboardingPostProcessor"/> so that successful onboardings auto-create the
/// initial TenantAdmin via M:N membership.
/// </summary>
public static class IdentityOnboardingExtensions
{
    /// <summary>
    /// Registers <see cref="TenantAdminAutoProvisioningPostProcessor"/> in the DI container
    /// so the Onboarding orchestrator picks it up and invokes it after each successful
    /// onboarding.
    /// </summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration).UseSqlServer(builder.Configuration)
    ///     .AddTenantAdminAutoProvisioning();
    ///
    /// builder.Services
    ///     .AddPolarOnboarding(builder.Configuration)
    ///     .UseEfTenantStoreSink();
    /// </code>
    /// </example>
    public static PolarIdentityBuilder AddTenantAdminAutoProvisioning(this PolarIdentityBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<IOnboardingPostProcessor, TenantAdminAutoProvisioningPostProcessor>();
        return builder;
    }
}
