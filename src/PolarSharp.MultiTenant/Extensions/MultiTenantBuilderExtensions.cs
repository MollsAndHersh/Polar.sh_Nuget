using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.Webhooks;

namespace PolarSharp.MultiTenant.Extensions;

/// <summary>
/// Extension methods on <see cref="PolarInfrastructureBuilder"/> for registering
/// PolarSharp multi-tenant services backed by Finbuckle.MultiTenant.
/// </summary>
public static class MultiTenantBuilderExtensions
{
    /// <summary>
    /// Registers Finbuckle multi-tenancy with the strategy and tenants configured in
    /// <c>PolarSharp:MultiTenant</c>, and wires per-tenant <see cref="PolarClient"/>
    /// caching via <see cref="IMultiTenantPolarClientFactory"/>.
    /// </summary>
    /// <param name="builder">The infrastructure builder returned by <c>AddPolarInfrastructure</c>.</param>
    /// <param name="configure">
    /// Optional delegate applied after configuration binding, allowing code-level overrides
    /// (e.g., adding tenants programmatically or changing the strategy at runtime).
    /// </param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// Configuration-only registration (recommended for most apps):
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant();
    /// </code>
    /// Programmatic tenant registration:
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant(opts =>
    ///     {
    ///         opts.Strategy = TenantStrategy.Hostname;
    ///         opts.Tenants.Add(new PolarTenantInfo
    ///         {
    ///             Id = "acme", Identifier = "acme.myapp.com",
    ///             PolarAccessToken = "tok_live_xxx"
    ///         });
    ///     });
    /// </code>
    /// </example>
    /// <remarks>
    /// <para>
    /// The tenant strategy and tenants are resolved eagerly at startup from
    /// <c>PolarSharp:MultiTenant</c> in <c>appsettings.json</c>, then the optional
    /// <paramref name="configure"/> delegate is applied. Both sources populate the same
    /// Finbuckle in-memory store.
    /// </para>
    /// <para>
    /// <see cref="IMultiTenantPolarClientFactory"/> is registered as a <c>Singleton</c>.
    /// Each tenant gets exactly one <see cref="PolarClient"/> (created lazily on first use,
    /// cached for the application lifetime). Under concurrent load, two threads racing for
    /// the same tenant are guaranteed to produce only one client instance.
    /// </para>
    /// <para>
    /// <c>UsePolarInfrastructure()</c> in the core package calls Finbuckle's
    /// <c>UseMultiTenancy()</c> automatically — the host app does not need a separate
    /// middleware call.
    /// </para>
    /// </remarks>
    public static PolarInfrastructureBuilder AddPolarMultiTenant(
        this PolarInfrastructureBuilder builder,
        Action<PolarMultiTenantOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Resolve options eagerly — strategy and tenants are startup-time settings; they
        // do not hot-reload, so reading IConfiguration directly is the correct approach here.
        var opts = new PolarMultiTenantOptions();
        builder.Configuration.GetSection("PolarSharp:MultiTenant").Bind(opts);
        configure?.Invoke(opts);

        // Register Finbuckle with the configured identification strategy.
        var finbuckleBuilder = builder.Services.AddMultiTenant<PolarTenantInfo>();

        ApplyStrategy(finbuckleBuilder, opts);

        // Populate the in-memory tenant store from the resolved options.
        finbuckleBuilder.WithInMemoryStore(storeOpts =>
        {
            foreach (var tenant in opts.Tenants)
                storeOpts.Tenants.Add(tenant);
        });

        // Each tenant gets its own SocketsHttpHandler + ResiliencePipeline built directly in
        // MultiTenantPolarClientFactory. No shared named HttpClient is needed here.
        builder.Services.TryAddSingleton<IMultiTenantPolarClientFactory, MultiTenantPolarClientFactory>();
        builder.Services.AddTransient<IStartupFilter, PolarMultiTenantStartupFilter>();

        // Webhook multi-tenant routing: resolve per-tenant HMAC secrets and set tenant context.
        // Build the resolver with a snapshot of the final opts (after configure delegate applied).
        var resolverOpts = opts;
        builder.Services.TryAddSingleton<IWebhookTenantResolver>(
            _ => new PolarMultiTenantWebhookResolver(resolverOpts));
        builder.Services.TryAddScoped<IWebhookTenantScopeInitializer, FinbuckleTenantScopeInitializer>();

        // Register the middleware hook under a well-known key so that UsePolarInfrastructure()
        // (in core, which has no reference to this package) can activate UseMultiTenancy()
        // without a hard coupling.
        builder.Services.AddKeyedSingleton<Action<IApplicationBuilder>>(
            "polar.multitenant.middleware",
            static (_, _) => static app => app.UseMultiTenant());

        SetMarkerFlag(builder.Services, m => m.MultiTenantRegistered = true);

        return builder;
    }

    // ── Strategy dispatch ─────────────────────────────────────────────────────────

    private static void ApplyStrategy(MultiTenantBuilder<PolarTenantInfo> finbuckleBuilder, PolarMultiTenantOptions opts)
    {
        switch (opts.Strategy)
        {
            case TenantStrategy.Route:
                finbuckleBuilder.WithRouteStrategy(opts.Route.Parameter, false);
                break;
            case TenantStrategy.Hostname:
                finbuckleBuilder.WithHostStrategy(opts.Hostname.Template);
                break;
            case TenantStrategy.Claim:
                finbuckleBuilder.WithClaimStrategy(opts.Claim.Type);
                break;
            default:
                finbuckleBuilder.WithHeaderStrategy(opts.Header.Name);
                break;
        }
    }

    // ── Marker mutation ───────────────────────────────────────────────────────────

    private static void SetMarkerFlag(
        IServiceCollection services,
        Action<PolarInfrastructureMarker> configure)
    {
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(PolarInfrastructureMarker)
              && d.ImplementationInstance is PolarInfrastructureMarker);

        if (descriptor?.ImplementationInstance is PolarInfrastructureMarker marker)
            configure(marker);
    }
}
