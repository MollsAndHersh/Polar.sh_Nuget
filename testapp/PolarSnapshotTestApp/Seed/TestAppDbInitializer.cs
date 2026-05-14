using Microsoft.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity;

namespace PolarSnapshotTestApp.Seed;

/// <summary>
/// Test-app Identity DB bring-up. Identity uses <c>EnsureCreatedAsync</c> for
/// simplicity (test app); Reporting DB migration is intentionally NOT run here.
/// </summary>
/// <remarks>
/// The Reporting DbContext (<c>PolarReportingDbContext</c>) extends
/// <c>TenantAwareDbContextBase</c>, which requires an active tenant in the per-scope
/// <c>IMultiTenantContextAccessor</c> at construction time. The orchestrator's
/// per-tick path uses <c>IPolarTenantScopeInitializer</c> to hydrate it — but
/// that interface ships without a concrete implementation in the codebase
/// (V20-005 Phase 2 design declared the abstraction; no host-default impl landed).
/// Until a default impl ships, hosts must (a) supply their own
/// <c>IPolarTenantScopeInitializer</c>, AND (b) handle Reporting DB migration
/// inside an HTTP request scope where Finbuckle has already hydrated the tenant.
/// This test app demonstrates the bridge's wiring + login/logout flow but does
/// NOT yet drive a real snapshot end-to-end against the Reporting DB.
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
