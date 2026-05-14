using System.Threading.Channels;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.Reporting.Identity.Extensions;
using PolarSharp.Reporting.Snapshot;

namespace PolarSharp.Reporting.Identity.Tests;

/// <summary>
/// V20-005 Phase 3 smoke tests for the bridge package's DI / pipeline registration
/// extensions. Verifies that <see cref="ReportingIdentityBuilderExtensions.AddPolarReportingIdentityHook"/>
/// correctly replaces the default <see cref="SignInManager{PolarApplicationUser}"/> with
/// the snapshot-aware decorator, and that
/// <see cref="ReportingIdentityBuilderExtensions.UsePolarReportingIdentityHeartbeat"/>
/// inserts the heartbeat middleware into the pipeline.
/// </summary>
/// <remarks>
/// Full end-to-end SignInManager-wrapper behavior (sign-in writes a cookie + the
/// orchestrator fires) requires the full ASP.NET Core authentication pipeline +
/// WebApplicationFactory; that lives in a future integration-test layer. These tests
/// cover the registration plumbing only — enough to catch DI breakage on every CI run.
/// </remarks>
public sealed class ReportingIdentityBuilderExtensionsTests
{
    [Fact]
    public void AddPolarReportingIdentityHook_replaces_the_SignInManager_with_the_snapshot_decorator()
    {
        using var sp = BuildIdentityWithBridge();
        using var scope = sp.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<SignInManager<PolarApplicationUser>>();

        Assert.IsType<PolarSnapshotSignInManager>(resolved);
    }

    [Fact]
    public void UsePolarReportingIdentityHeartbeat_inserts_the_middleware_in_the_pipeline()
    {
        var sp = BuildIdentityWithBridge();
        var app = new ApplicationBuilder(sp);

        // Just verifying the extension method runs cleanly — building the pipeline pulls
        // the middleware into the chain. A failure to register/resolve would throw here.
        var ex = Record.Exception(() => app.UsePolarReportingIdentityHeartbeat().Build());
        Assert.Null(ex);
    }

    // ── Harness ────────────────────────────────────────────────────────────

    private static ServiceProvider BuildIdentityWithBridge()
    {
        var conn = new SqliteConnection("Filename=:memory:");
        conn.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<PolarUserDbContext>(opt => opt.UseSqlite(conn));
        services.AddIdentity<PolarApplicationUser, PolarApplicationRole>()
            .AddEntityFrameworkStores<PolarUserDbContext>()
            .AddDefaultTokenProviders();

        // Bridge dependencies: orchestrator + tenant accessor + options.
        services.AddSingleton<IReportSnapshotTrigger, NoOpTrigger>();
        services.AddSingleton<IMultiTenantContextAccessor<PolarTenantInfo>>(_ => new EmptyAccessor());
        services.AddSingleton(Options.Create(new SnapshotTriggerOptions()));
        services.AddHttpContextAccessor();

        services.AddPolarReportingIdentityHook();
        return services.BuildServiceProvider();
    }

    private sealed class EmptyAccessor : IMultiTenantContextAccessor<PolarTenantInfo>
    {
        public IMultiTenantContext<PolarTenantInfo> MultiTenantContext { get; set; } =
            new MultiTenantContext<PolarTenantInfo>(new PolarTenantInfo());

        IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => MultiTenantContext;
    }

    private sealed class NoOpTrigger : IReportSnapshotTrigger
    {
        public Task<SnapshotCompletedEvent> TriggerImmediateAsync(string tenantId, string reason, CancellationToken ct = default) => Task.FromResult(new SnapshotCompletedEvent
        {
            TenantId = tenantId, Reason = reason, CompletedAt = DateTimeOffset.UtcNow,
            Success = true, ResourcesIngested = 0, RowsIngested = 0, Duration = TimeSpan.Zero,
        });
        public Task StartPeriodicAsync(string tenantId, TimeSpan interval) => Task.CompletedTask;
        public Task StopPeriodicAsync(string tenantId) => Task.CompletedTask;
        public void Heartbeat(string tenantId) { }
        public DateTimeOffset? GetLastSnapshotAt(string tenantId) => null;
        public TimeSpan? GetTimeUntilNextSnapshot(string tenantId) => null;
        public IAsyncEnumerable<SnapshotCompletedEvent> CompletedEventsAsync(CancellationToken ct) =>
            Channel.CreateBounded<SnapshotCompletedEvent>(1).Reader.ReadAllAsync(ct);
    }
}
