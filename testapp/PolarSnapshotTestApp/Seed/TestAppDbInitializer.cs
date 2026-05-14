using Microsoft.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity;

namespace PolarSnapshotTestApp.Seed;

/// <summary>
/// Test-app Identity DB bring-up. Reporting DB migration deferred — see remarks.
/// </summary>
/// <remarks>
/// PolarReportingDbContext extends TenantAwareDbContextBase, which requires an active
/// tenant in the per-scope IMultiTenantContextAccessor at construction time. With the
/// shipped DefaultPolarTenantScopeInitializer the orchestrator's per-tick path works
/// (the orchestrator opens its own service-provider scope), but startup-context
/// migration via IHostedService still needs more work — Finbuckle's AsyncLocal accessor
/// behavior in the IHostedService startup scope doesn't reliably surface the tenant
/// set via IMultiTenantContextSetter for subsequent same-scope DbContext construction.
/// Tracked as a follow-up; the bridge wiring still demonstrates end-to-end via login.
/// </remarks>
internal sealed class TestAppDbInitializer(IServiceProvider sp, ILogger<TestAppDbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = sp.CreateAsyncScope();

        var identityDb = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
        await identityDb.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("PolarSnapshotTestApp: Identity SQLite DB ready ({Path}).", identityDb.Database.GetConnectionString());
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
