using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity.Authorization;

namespace PolarSharp.MultiTenant.Identity.Extensions;

/// <summary>
/// DI registration helpers for PolarSharp Identity.
/// </summary>
/// <remarks>
/// <para>
/// The base registration adds the user/role manager wiring, authorization policies,
/// <see cref="ICurrentUser"/> resolution, the AppMasterAdmin bootstrapper, the TenantAdmin
/// invariant validator, and the cross-tenant signal. The actual DbContext provider
/// (SQL Server / SQLite / PostgreSQL) is selected by calling one of the
/// <c>UseXxx()</c> extension methods on the returned <see cref="PolarIdentityBuilder"/>.
/// </para>
/// </remarks>
public static class IdentityBuilderExtensions
{
    /// <summary>Registers the PolarSharp Identity package with default options.</summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration — bound to <see cref="PolarIdentityOptions"/>.</param>
    /// <returns>A builder for chaining provider registration (<c>UseSqlServer</c>, etc.).</returns>
    public static PolarIdentityBuilder AddPolarIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<PolarIdentityOptions>()
            .Bind(configuration.GetSection(PolarIdentityOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpContextAccessor();

        // ── Permission resolution ────────────────────────────────────────
        services.TryAddSingleton<IRolePermissionResolver>(_ => new DefaultRolePermissionResolver());

        // ── Per-request signal + ICurrentUser ────────────────────────────
        services.AddScoped<AppMasterAdminCrossTenantSignal>();
        services.AddScoped<IAppMasterAdminCrossTenantSignal>(sp => sp.GetRequiredService<AppMasterAdminCrossTenantSignal>());
        services.AddScoped<IAppMasterAdminCrossTenantContext>(sp => sp.GetRequiredService<AppMasterAdminCrossTenantSignal>());
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();

        // ── Authorization policies + handlers ────────────────────────────
        services.AddAuthorization(PolarAuthorizationPolicies.RegisterAllBuiltIn);
        services.TryAddScoped<IAuthorizationHandler, AppMasterAdminAuthorizationHandler>();
        services.TryAddScoped<IAuthorizationHandler, PolarPermissionAuthorizationHandler>();

        // ── Provisioning + bootstrap + invariant validator ──────────────
        services.TryAddScoped<IAppMasterAdminProvisioning, AppMasterAdminProvisioning>();
        services.AddHostedService<RoleSeeder>();
        services.AddHostedService<AppMasterAdminBootstrapper>();
        services.AddHostedService<TenantAdminInvariantValidator>();

        return new PolarIdentityBuilder(services);
    }
}

/// <summary>
/// Fluent builder returned by <see cref="IdentityBuilderExtensions.AddPolarIdentity"/>.
/// Provider packages (<c>.SqlServer</c>, <c>.Sqlite</c>, <c>.PostgreSQL</c>) extend this with
/// concrete <c>UseSqlServer</c> / <c>UseSqlite</c> / <c>UsePostgreSql</c> methods.
/// </summary>
public sealed class PolarIdentityBuilder
{
    /// <summary>The underlying DI service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new builder.</summary>
    /// <param name="services">The DI service collection.</param>
    public PolarIdentityBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>Adds Identity-managed user, role, and SignInManager services on top of the registered <see cref="PolarUserDbContext"/>.</summary>
    /// <remarks>
    /// Provider extensions (<c>UseSqlServer</c>, etc.) call this internally after wiring the
    /// DbContext. Hosts can also call it directly to customize the <see cref="IdentityOptions"/>.
    /// </remarks>
    public PolarIdentityBuilder AddCoreIdentityServices(Action<IdentityOptions>? configureIdentity = null)
    {
        var idBuilder = Services
            .AddIdentityCore<PolarApplicationUser>(opts =>
            {
                opts.User.RequireUniqueEmail = true;
                opts.Password.RequiredLength = 12;
                opts.Password.RequireDigit = true;
                opts.Password.RequireLowercase = true;
                opts.Password.RequireUppercase = true;
                opts.Password.RequireNonAlphanumeric = true;
                opts.Lockout.MaxFailedAccessAttempts = 5;
                opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                configureIdentity?.Invoke(opts);
            })
            .AddRoles<PolarApplicationRole>()
            .AddEntityFrameworkStores<PolarUserDbContext>()
            .AddDefaultTokenProviders();

        return this;
    }
}
