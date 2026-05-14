using Microsoft.EntityFrameworkCore;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.Reporting.EntityFrameworkCore;

namespace PolarSnapshotTestApp.Seed;

/// <summary>
/// Test-app DB bring-up. Identity uses <c>EnsureCreatedAsync</c> for simplicity;
/// Reporting uses the full migration set so snapshot tables match production schema.
/// </summary>
/// <remarks>
/// <c>PolarReportingDbContext</c> extends <c>TenantAwareDbContextBase</c>, which
/// requires an active tenant in the per-scope <c>IMultiTenantContextAccessor</c> at
/// construction time. We resolve a bootstrap tenant via <c>IPolarTenantScopeInitializer</c>
/// (async) and apply it to the scope synchronously via <see cref="TenantScopeExtensions.SetCurrentTenant"/>
/// — this keeps the AsyncLocal mutation in this method's frame so the next
/// <c>GetRequiredService&lt;PolarReportingDbContext&gt;()</c> sees the tenant.
/// </remarks>
internal sealed class TestAppDbInitializer(IServiceProvider sp, ILogger<TestAppDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = sp.CreateAsyncScope();

        var identityDb = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
        await identityDb.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("PolarSnapshotTestApp: Identity SQLite DB ready ({Path}).", identityDb.Database.GetConnectionString());

        var initializer = scope.ServiceProvider.GetRequiredService<IPolarTenantScopeInitializer>();
        var tenant = await initializer.ResolveTenantAsync("00000000-0000-0000-0000-000000000001", cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            logger.LogWarning("PolarSnapshotTestApp: bootstrap tenant not found; skipping Reporting DB migration.");
            return;
        }
        scope.ServiceProvider.SetCurrentTenant(tenant);

        var reportingDb = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        await reportingDb.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("PolarSnapshotTestApp: Reporting SQLite DB migrated ({Path}).", reportingDb.Database.GetConnectionString());
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
