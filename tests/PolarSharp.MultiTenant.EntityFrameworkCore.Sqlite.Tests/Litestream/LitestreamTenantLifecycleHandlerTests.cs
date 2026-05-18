using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests.Litestream;

/// <summary>
/// Tests for <see cref="LitestreamTenantLifecycleHandler"/> — the MediatR notification
/// handler that bridges <see cref="TenantStatusChangedNotification"/> events into
/// <see cref="LitestreamRegenCoordinator"/> exclusion-set mutations.
/// </summary>
/// <remarks>
/// <para>
/// The handler is a no-op when either <see cref="LitestreamOptions.UseLitestream"/> or
/// <see cref="LitestreamOptions.AutoRegenerateOnTenantChange"/> is <c>false</c>.
/// When both are on, the mapping is:
/// </para>
/// <list type="bullet">
///   <item>NewStatus == Active → coordinator.RemoveExclusion(tenantId)</item>
///   <item>Anything else (Suspended / Inactive / Deleted) → coordinator.AddExclusion(tenantId, reason)</item>
/// </list>
/// <para>
/// Because <see cref="LitestreamRegenCoordinator"/> is a sealed-by-shape concrete with
/// non-virtual mutators, the tests use a real coordinator and assert post-conditions on
/// its <see cref="LitestreamRegenCoordinator.GetCurrentExclusions"/> snapshot rather than
/// counting recorded calls on a stub.
/// </para>
/// </remarks>
public sealed class LitestreamTenantLifecycleHandlerTests
{
    // --- No-op branches ---------------------------------------------------------------

    [Fact]
    public async Task Handle_does_nothing_when_UseLitestream_is_false()
    {
        var coord = NewCoordinator();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.UseLitestream = false;
        opts.AutoRegenerateOnTenantChange = true;
        var sut = NewSut(coord, opts);
        var tenantId = Guid.NewGuid();

        await sut.Handle(NewNotification(TenantStatus.Active, TenantStatus.Suspended, tenantId: tenantId), CancellationToken.None);

        Assert.Empty(coord.GetCurrentExclusions());
    }

    [Fact]
    public async Task Handle_does_nothing_when_AutoRegenerateOnTenantChange_is_false()
    {
        var coord = NewCoordinator();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.UseLitestream = true;
        opts.AutoRegenerateOnTenantChange = false;
        var sut = NewSut(coord, opts);
        var tenantId = Guid.NewGuid();

        await sut.Handle(NewNotification(TenantStatus.Active, TenantStatus.Suspended, tenantId: tenantId), CancellationToken.None);

        Assert.Empty(coord.GetCurrentExclusions());
    }

    // --- AddExclusion branches --------------------------------------------------------

    [Fact]
    public async Task Handle_calls_AddExclusion_for_Active_to_Suspended_transition()
    {
        var coord = NewCoordinator();
        var sut = NewSut(coord, EnabledOptions());
        var tenantId = Guid.NewGuid();

        await sut.Handle(NewNotification(TenantStatus.Active, TenantStatus.Suspended, tenantId: tenantId), CancellationToken.None);

        Assert.Contains(tenantId, coord.GetCurrentExclusions());
    }

    [Fact]
    public async Task Handle_calls_AddExclusion_for_Active_to_Inactive_transition()
    {
        var coord = NewCoordinator();
        var sut = NewSut(coord, EnabledOptions());
        var tenantId = Guid.NewGuid();

        await sut.Handle(NewNotification(TenantStatus.Active, TenantStatus.Inactive, tenantId: tenantId), CancellationToken.None);

        Assert.Contains(tenantId, coord.GetCurrentExclusions());
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Inactive)]
    public async Task Handle_calls_AddExclusion_for_any_to_Deleted_transition(TenantStatus previous)
    {
        var coord = NewCoordinator();
        var sut = NewSut(coord, EnabledOptions());
        var tenantId = Guid.NewGuid();

        await sut.Handle(NewNotification(previous, TenantStatus.Deleted, tenantId: tenantId), CancellationToken.None);

        Assert.Contains(tenantId, coord.GetCurrentExclusions());
    }

    // --- RemoveExclusion branches -----------------------------------------------------

    [Fact]
    public async Task Handle_calls_RemoveExclusion_for_Suspended_to_Active_transition()
    {
        var coord = NewCoordinator();
        var tenantId = Guid.NewGuid();
        // Pre-seed the exclusion so the handler's RemoveExclusion path has something to remove.
        coord.AddExclusion(tenantId, "pre-seed for test");

        var sut = NewSut(coord, EnabledOptions());

        await sut.Handle(NewNotification(TenantStatus.Suspended, TenantStatus.Active, tenantId: tenantId), CancellationToken.None);

        Assert.DoesNotContain(tenantId, coord.GetCurrentExclusions());
    }

    [Fact]
    public async Task Handle_calls_RemoveExclusion_for_Inactive_to_Active_transition()
    {
        var coord = NewCoordinator();
        var tenantId = Guid.NewGuid();
        coord.AddExclusion(tenantId, "pre-seed for test");

        var sut = NewSut(coord, EnabledOptions());

        await sut.Handle(NewNotification(TenantStatus.Inactive, TenantStatus.Active, tenantId: tenantId), CancellationToken.None);

        Assert.DoesNotContain(tenantId, coord.GetCurrentExclusions());
    }

    // --- Logging ----------------------------------------------------------------------

    [Fact]
    public async Task Handle_logs_action_description_with_transition_and_reason()
    {
        var coord = NewCoordinator();
        var log = new RecordingLogger<LitestreamTenantLifecycleHandler>();
        var sut = NewSut(coord, EnabledOptions(), log);

        await sut.Handle(
            NewNotification(TenantStatus.Active, TenantStatus.Suspended, reason: "billing dispute - manual review"),
            CancellationToken.None);

        Assert.Contains(log.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("Suspended", StringComparison.Ordinal));
    }

    // --- Helpers ----------------------------------------------------------------------

    private static LitestreamRegenCoordinator NewCoordinator()
        => new(NullLogger<LitestreamRegenCoordinator>.Instance);

    private static LitestreamOptions EnabledOptions()
    {
        var opts = TestHelpers.FullyEnabledOptions();
        opts.AutoRegenerateOnTenantChange = true;
        opts.ConfigOutputPath = "/etc/litestream.yml";
        opts.LitestreamPidFilePath = "/var/run/litestream.pid";
        return opts;
    }

    private static LitestreamTenantLifecycleHandler NewSut(
        LitestreamRegenCoordinator coord,
        LitestreamOptions opts,
        ILogger<LitestreamTenantLifecycleHandler>? log = null)
    {
        return new LitestreamTenantLifecycleHandler(
            coord,
            new StaticOptionsMonitor<LitestreamOptions>(opts),
            log ?? NullLogger<LitestreamTenantLifecycleHandler>.Instance);
    }

    private static TenantStatusChangedNotification NewNotification(
        TenantStatus previous,
        TenantStatus next,
        Guid? tenantId = null,
        string reason = "test")
    {
        return new TenantStatusChangedNotification
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            TenantIdentifier = "tenant-x",
            TenantName = "Tenant X",
            PreviousStatus = previous,
            NewStatus = next,
            Reason = reason,
            ActorUserId = null,
            OccurredAt = DateTimeOffset.UtcNow,
            SiteManagerEmail = "ops@example.com",
            SiteManagerEmailVerified = true,
            SiteManagerPhone = null,
        };
    }
}
