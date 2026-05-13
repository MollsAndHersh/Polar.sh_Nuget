using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Startup invariant — every tenant must have at least one ACTIVE
/// <see cref="PolarRoles.TenantAdmin"/> membership.
/// </summary>
/// <remarks>
/// <para>
/// AppMasterAdmins are NOT counted toward this invariant. Each tenant requires its OWN
/// dedicated administrator — a tenant cannot be "owned" by SaaS-provider staff alone.
/// </para>
/// <para>
/// When <see cref="PolarIdentityOptions.RequireTenantAdminInvariant"/> is <see langword="true"/>
/// (the default), a violation throws on startup and blocks the host. Set the option to
/// <see langword="false"/> to log a warning and continue (useful during migrations).
/// </para>
/// </remarks>
public sealed class TenantAdminInvariantValidator : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PolarIdentityOptions> _options;
    private readonly ILogger<TenantAdminInvariantValidator> _logger;

    /// <summary>Initializes a new validator.</summary>
    public TenantAdminInvariantValidator(
        IServiceScopeFactory scopeFactory,
        IOptions<PolarIdentityOptions> options,
        ILogger<TenantAdminInvariantValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var tenantStore = scope.ServiceProvider.GetService<IMultiTenantStore<PolarTenantInfo>>();
        if (tenantStore is null)
        {
            _logger.LogDebug("TenantAdminInvariantValidator: no tenant store registered — skipping check.");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PolarApplicationRole>>();

        var tenantAdminRole = await roleManager.FindByNameAsync(PolarRoles.TenantAdmin).ConfigureAwait(false);
        if (tenantAdminRole is null)
        {
            _logger.LogWarning("TenantAdminInvariantValidator: TenantAdmin role not seeded yet — skipping check (RoleSeeder may not have run).");
            return;
        }

        var allTenants = await tenantStore.GetAllAsync().ConfigureAwait(false);
        var violations = new List<string>();

        foreach (var tenant in allTenants)
        {
            if (tenant.Id is null) continue;
            if (!Guid.TryParse(tenant.Id, out var tenantGuid))
            {
                _logger.LogWarning("TenantAdminInvariantValidator: tenant id {TenantId} is not a valid Guid — skipping.", tenant.Id);
                continue;
            }

            var hasActiveAdmin = await db.Memberships
                .IgnoreQueryFilters()  // we need to read across all tenants to validate
                .AnyAsync(m =>
                    m.TenantId == tenantGuid &&
                    m.RoleId == tenantAdminRole.Id &&
                    m.IsActive,
                    cancellationToken).ConfigureAwait(false);

            if (!hasActiveAdmin)
            {
                violations.Add($"{tenant.Id} ({tenant.Identifier})");
            }
        }

        if (violations.Count == 0) return;

        var message = $"Tenants with no active TenantAdmin membership: {string.Join(", ", violations)}. " +
                      "Run onboarding for these tenants OR set PolarSharp:Identity:RequireTenantAdminInvariant=false to continue.";

        if (_options.Value.RequireTenantAdminInvariant)
        {
            _logger.LogCritical("TenantAdminInvariantValidator: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogWarning("TenantAdminInvariantValidator (warning mode): {Message}", message);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
