using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// One-time helper that seeds the tenants table from <c>appsettings.json:PolarSharp:MultiTenant:Tenants</c>
/// on first startup when the table is empty. Runs as an <see cref="IHostedService"/> before
/// the host accepts any traffic.
/// </summary>
/// <remarks>
/// <para>
/// Use case: migrating from the v1.1.0 appsettings-only tenant registry to a SQL-backed
/// store. Set <c>seedFromAppSettings: true</c> on the provider's <c>.UseXxx()</c> call. After
/// the seed completes successfully, remove the <c>Tenants</c> array from
/// <c>appsettings.json</c> — the SQL table is now the source of truth.
/// </para>
/// <para>
/// Idempotent: if the tenants table already has rows, the seeder logs and exits without
/// touching the database.
/// </para>
/// </remarks>
public sealed class AppSettingsSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PolarMultiTenantOptions> _options;
    private readonly ILogger<AppSettingsSeeder> _logger;

    /// <summary>Initializes the seeder with the supplied dependencies.</summary>
    public AppSettingsSeeder(
        IServiceScopeFactory scopeFactory,
        IOptions<PolarMultiTenantOptions> options,
        ILogger<AppSettingsSeeder> logger)
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
        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        if (await db.Tenants.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("PolarSharp.MultiTenant: SQL tenant table already populated — skipping appsettings seed.");
            return;
        }

        var configuredTenants = _options.Value.Tenants ?? [];
        if (configuredTenants.Count == 0)
        {
            _logger.LogInformation("PolarSharp.MultiTenant: no tenants in appsettings to seed.");
            return;
        }

        foreach (var t in configuredTenants)
        {
            db.Tenants.Add(new PolarTenantInfoEntity
            {
                Id = t.Id,
                Identifier = t.Identifier,
                Name = t.Name,
                PolarAccessToken = t.PolarAccessToken,
                Server = t.Server,
                OnboardedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "PolarSharp.MultiTenant: seeded {Count} tenants from appsettings.json into SQL store. " +
            "You can now remove the PolarSharp:MultiTenant:Tenants array from appsettings.json.",
            configuredTenants.Count);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
