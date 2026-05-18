using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb.Upgrade;

/// <summary>
/// Cosmos-equivalent of the <c>AddUpgradeHistoryTable</c> EF migration shipped by the
/// relational providers. Ensures the <c>polar_upgrade_history</c> container exists at
/// host startup by invoking
/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade"/>'s <c>EnsureCreatedAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why a hosted service instead of a Migration class.</strong> Cosmos has no
/// schema-migration concept — there is no equivalent of <c>__EFMigrationsHistory</c>, no
/// versioned DDL, and no <c>dotnet ef database update</c>. Container creation is the
/// EF Cosmos provider's responsibility, and the canonical place to trigger it is
/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade"/>'s <c>EnsureCreatedAsync</c>.
/// This hosted service runs once at host startup before any other component touches the
/// upgrade-history container.
/// </para>
/// <para>
/// <strong>Indexing policy.</strong> The default EF Cosmos provider indexing policy
/// indexes every property — sufficient for the upgrade-history container's tiny row count
/// (one row per upgrade kind per deployment, lifetime). No custom indexing policy is
/// needed; if one becomes necessary as upgrade kinds accumulate, configure it via
/// <see cref="PolarTenantDbContext.OnModelCreating(ModelBuilder)"/> override in a derived
/// context.
/// </para>
/// </remarks>
internal sealed class CosmosUpgradeHistoryContainerProvisioner : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CosmosUpgradeHistoryContainerProvisioner> _logger;

    /// <summary>Initializes a new <see cref="CosmosUpgradeHistoryContainerProvisioner"/>.</summary>
    /// <param name="services">DI root used to resolve a scoped <see cref="PolarTenantDbContext"/>.</param>
    /// <param name="logger">Logger.</param>
    public CosmosUpgradeHistoryContainerProvisioner(
        IServiceProvider services,
        ILogger<CosmosUpgradeHistoryContainerProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        try
        {
            var created = await db.Database
                .EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "PolarSharp Cosmos upgrade-history container provisioner: EnsureCreated returned {Created}.",
                created);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PolarSharp Cosmos upgrade-history container provisioner: EnsureCreated failed. " +
                "The upgrade migrator will not be able to run until the container exists.");
            throw;
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
