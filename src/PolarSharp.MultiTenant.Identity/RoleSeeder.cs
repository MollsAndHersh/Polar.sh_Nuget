using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Seeds the built-in PolarSharp roles (<see cref="PolarRoles"/>) on first startup.
/// </summary>
/// <remarks>
/// Idempotent: roles already present in the database are left untouched. Built-in roles are
/// flagged with <see cref="PolarApplicationRole.IsBuiltIn"/> = <see langword="true"/>; the
/// <see cref="PolarRoles.AppMasterAdmin"/> role additionally carries
/// <see cref="PolarApplicationRole.IsSiteLevel"/> = <see langword="true"/>.
/// </remarks>
public sealed class RoleSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RoleSeeder> _logger;

    /// <summary>Initializes a new role seeder.</summary>
    public RoleSeeder(IServiceScopeFactory scopeFactory, ILogger<RoleSeeder> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PolarApplicationRole>>();

        foreach (var name in PolarRoles.All)
        {
            if (await roleManager.RoleExistsAsync(name).ConfigureAwait(false)) continue;

            var role = new PolarApplicationRole(name)
            {
                IsBuiltIn = true,
                IsSiteLevel = name == PolarRoles.AppMasterAdmin,
                Description = DescriptionFor(name),
            };
            var result = await roleManager.CreateAsync(role).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Failed to seed built-in role {Role}: {Errors}",
                    name,
                    string.Join("; ", result.Errors.Select(e => $"{e.Code}={e.Description}")));
            }
            else
            {
                _logger.LogInformation("Seeded built-in role {Role}", name);
            }
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string DescriptionFor(string roleName) => roleName switch
    {
        PolarRoles.AppMasterAdmin => "SITE-LEVEL — SaaS provider's own staff. Cross-tenant access via [AllowCrossTenant] opt-in.",
        PolarRoles.TenantAdmin    => "TENANT-LEVEL — full administrative access within ONE tenant.",
        PolarRoles.TenantUser     => "TENANT-LEVEL — day-to-day operational access within ONE tenant.",
        PolarRoles.ReadOnly       => "TENANT-LEVEL — read-only access within ONE tenant.",
        PolarRoles.Auditor        => "TENANT-LEVEL — read access plus audit log inspection within ONE tenant.",
        _                         => "PolarSharp built-in role.",
    };
}
