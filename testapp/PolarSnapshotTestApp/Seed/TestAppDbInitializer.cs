using Microsoft.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.Reporting.EntityFrameworkCore;

namespace PolarSnapshotTestApp.Seed;

/// <summary>
/// Test-app DB bring-up. Runs at startup before <see cref="TestUserSeeder"/>.
/// Identity uses <c>EnsureCreatedAsync</c> for simplicity (test app); Reporting
/// uses the full migration set so the snapshot tables match production schema.
/// </summary>
internal sealed class TestAppDbInitializer(IServiceProvider sp, ILogger<TestAppDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = sp.CreateAsyncScope();

        var identityDb = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
        await identityDb.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("PolarSnapshotTestApp: Identity SQLite DB ready ({Path}).", identityDb.Database.GetConnectionString());

        var reportingDb = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        await reportingDb.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("PolarSnapshotTestApp: Reporting SQLite DB migrated ({Path}).", reportingDb.Database.GetConnectionString());
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
