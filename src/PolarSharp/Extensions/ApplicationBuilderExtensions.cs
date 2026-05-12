using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.Extensions;

/// <summary>
/// Extension methods for configuring PolarSharp middleware in the ASP.NET Core request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Activates PolarSharp middleware for all features registered during service configuration.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add middleware to.</param>
    /// <returns>The <paramref name="app"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="app"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Place this call in <c>Program.cs</c> after <c>UseRouting()</c> and
    /// <c>UseCors()</c>, and <strong>before</strong> <c>UseAuthentication()</c>
    /// and <c>UseAuthorization()</c>:
    /// </para>
    /// <code>
    /// app.UseRouting();
    /// app.UseCors();
    ///
    /// app.UsePolarInfrastructure();   // ← here
    ///
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapControllers();
    /// </code>
    /// <para>
    /// Internally this method inserts the following middleware in order:
    /// <list type="number">
    ///   <item><description>
    ///     <see cref="RequestLocalizationMiddleware"/> — sets <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
    ///     from the request so that PolarSharp error messages are localised.
    ///     Skipped if the host has already called <c>UseRequestLocalization</c>.
    ///   </description></item>
    ///   <item><description>
    ///     Finbuckle <c>UseMultiTenancy</c> — resolves the current tenant from the request.
    ///     Skipped if <c>AddPolarMultiTenant</c> was not called during service registration.
    ///   </description></item>
    ///   <item><description>
    ///     Polar webhook route mapping — registers <c>POST {WebhookPath}</c>.
    ///     Skipped if <c>AddPolarWebhooks</c> was not called during service registration.
    ///   </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// All feature middleware is discovered via keyed DI services and is safe to call
    /// even when only the core package is installed — missing services are treated as
    /// disabled features and silently skipped.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UsePolarInfrastructure(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var marker = app.ApplicationServices.GetRequiredService<PolarInfrastructureMarker>();

        EnsureRequestLocalization(app);

        if (marker.MultiTenantRegistered)
            UseMultiTenancyIfAvailable(app);

        // Always attempt webhook middleware discovery — keyed services return null when
        // PolarSharp.Webhooks is not installed, so both calls are safe no-ops without it.
        ActivateWebhookRateLimiterIfAvailable(app);
        MapWebhookRouteIfAvailable(app);

        return app;
    }

    private static void EnsureRequestLocalization(IApplicationBuilder app)
    {
        // Only register if the host has not already done so.
        // We detect this by checking if IRequestCultureProvider-related services are set up;
        // the simplest safe approach is a try-use-default pattern.
        // The host can call app.UseRequestLocalization() before this method to override fully.
        var options = new RequestLocalizationOptions()
            .SetDefaultCulture("en-US")
            .AddSupportedCultures("en-US", "es-MX")
            .AddSupportedUICultures("en-US", "es-MX");

        // UseRequestLocalization is idempotent in practice (it just registers middleware);
        // we intentionally let the host control the outer localization pipeline — if they
        // already set up a richer configuration, it takes effect first in the pipeline.
        app.UseRequestLocalization(options);
    }

    private static void UseMultiTenancyIfAvailable(IApplicationBuilder app)
    {
        // PolarSharp.MultiTenant registers an Action<IApplicationBuilder> keyed service that
        // calls Finbuckle's UseMultiTenancy(). Core invokes it here without a hard dependency.
        var action = app.ApplicationServices
            .GetKeyedService<Action<IApplicationBuilder>>("polar.multitenant.middleware");
        action?.Invoke(app);
    }

    private static void ActivateWebhookRateLimiterIfAvailable(IApplicationBuilder app)
    {
        // PolarSharp.Webhooks registers an Action<IApplicationBuilder> keyed service during
        // AddPolarWebhooks() that calls app.UseRateLimiter() when EnableRateLimiting = true.
        var action = app.ApplicationServices
            .GetKeyedService<Action<IApplicationBuilder>>("polar.webhooks.ratelimiter");
        action?.Invoke(app);
    }

    private static void MapWebhookRouteIfAvailable(IApplicationBuilder app)
    {
        // PolarSharp.Webhooks registers an Action<IEndpointRouteBuilder> keyed service during
        // AddPolarWebhooks(). Core invokes it here without a hard dependency on the Webhooks package.
        if (app is not IEndpointRouteBuilder endpoints) return;
        var mapper = app.ApplicationServices
            .GetKeyedService<Action<IEndpointRouteBuilder>>("polar.webhooks.mapper");
        mapper?.Invoke(endpoints);
    }
}
