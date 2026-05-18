using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests.Litestream;

/// <summary>
/// Tests for <see cref="LitestreamRegenCoordinator"/> — the singleton that owns the
/// excluded-tenant set and the shared regen-signal channel.
/// </summary>
/// <remarks>
/// <para>
/// The coordinator's contract: mutate-and-signal pairs (AddExclusion / RemoveExclusion)
/// snapshot the set to an immutable <see cref="System.Collections.Frozen.FrozenSet{T}"/>
/// and publish via volatile-reference replacement. Snapshots returned to callers are
/// truly immutable — subsequent mutations to the coordinator must not be visible through
/// a previously-handed-out snapshot.
/// </para>
/// <para>
/// The bulk-seed entry point used at startup writes the set in one atomic swap WITHOUT
/// per-item signal noise; this is verified explicitly so future contributors don't
/// accidentally regress to per-item signalling.
/// </para>
/// </remarks>
public sealed class LitestreamRegenCoordinatorTests
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(2);

    // --- Initial state ----------------------------------------------------------------

    [Fact]
    public void GetCurrentExclusions_returns_empty_set_initially()
    {
        var sut = NewSut();

        Assert.Empty(sut.GetCurrentExclusions());
    }

    // --- AddExclusion -----------------------------------------------------------------

    [Fact]
    public async Task AddExclusion_adds_to_set_and_signals_regen()
    {
        var sut = NewSut();
        var tenantId = Guid.NewGuid();

        var added = sut.AddExclusion(tenantId, "test reason");

        Assert.True(added);
        Assert.Contains(tenantId, sut.GetCurrentExclusions());

        var signal = await ReadSignalAsync(sut);
        Assert.Equal(RegenTrigger.TenantExcluded, signal.Trigger);
        Assert.Equal(tenantId, signal.TenantId);
    }

    [Fact]
    public async Task AddExclusion_is_idempotent()
    {
        var sut = NewSut();
        var tenantId = Guid.NewGuid();

        var first = sut.AddExclusion(tenantId, "first");
        var second = sut.AddExclusion(tenantId, "second");

        Assert.True(first);
        Assert.False(second);
        Assert.Single(sut.GetCurrentExclusions());

        // First call should have written a signal. Second is a no-op (no second signal).
        var signal = await ReadSignalAsync(sut);
        Assert.Equal(tenantId, signal.TenantId);
        await AssertNoMoreSignalsAsync(sut);
    }

    // --- RemoveExclusion --------------------------------------------------------------

    [Fact]
    public async Task RemoveExclusion_removes_from_set_and_signals_regen()
    {
        var sut = NewSut();
        var tenantId = Guid.NewGuid();
        sut.AddExclusion(tenantId, "seed");
        await ReadSignalAsync(sut); // drain the Add signal

        var removed = sut.RemoveExclusion(tenantId);

        Assert.True(removed);
        Assert.DoesNotContain(tenantId, sut.GetCurrentExclusions());

        var signal = await ReadSignalAsync(sut);
        Assert.Equal(RegenTrigger.TenantReincluded, signal.Trigger);
        Assert.Equal(tenantId, signal.TenantId);
    }

    [Fact]
    public async Task RemoveExclusion_when_not_present_is_a_noop()
    {
        var sut = NewSut();

        var removed = sut.RemoveExclusion(Guid.NewGuid());

        Assert.False(removed);
        await AssertNoMoreSignalsAsync(sut);
    }

    // --- SeedExclusions ---------------------------------------------------------------

    [Fact]
    public async Task SeedExclusions_bulk_adds_without_per_item_signals()
    {
        var sut = NewSut();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();

        sut.SeedExclusions(ids);

        Assert.Equal(5, sut.GetCurrentExclusions().Count);
        foreach (var id in ids) Assert.Contains(id, sut.GetCurrentExclusions());

        // Per the design, SeedExclusions does NOT fire signals — the hosted service's
        // startup-initial-sync handles the initial regen separately.
        await AssertNoMoreSignalsAsync(sut);
    }

    // --- Snapshot immutability --------------------------------------------------------

    [Fact]
    public async Task GetCurrentExclusions_returns_immutable_snapshot()
    {
        var sut = NewSut();
        var first = Guid.NewGuid();
        sut.AddExclusion(first, "first");
        await ReadSignalAsync(sut);

        // Take a snapshot reference now.
        var snapshot = sut.GetCurrentExclusions();
        Assert.Single(snapshot);

        // Add another exclusion — the previously taken snapshot must not reflect this.
        sut.AddExclusion(Guid.NewGuid(), "second");

        Assert.Single(snapshot);
        Assert.Equal(2, sut.GetCurrentExclusions().Count);
    }

    // --- Concurrency safety -----------------------------------------------------------

    [Fact]
    public async Task Concurrent_AddExclusion_calls_from_multiple_threads_are_safe()
    {
        var sut = NewSut();
        const int ConcurrentCount = 100;
        var ids = Enumerable.Range(0, ConcurrentCount).Select(_ => Guid.NewGuid()).ToArray();

        var tasks = ids
            .Select(id => Task.Run(() => sut.AddExclusion(id, "concurrent")))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(ConcurrentCount, sut.GetCurrentExclusions().Count);
        foreach (var id in ids) Assert.Contains(id, sut.GetCurrentExclusions());
    }

    // --- Helpers ----------------------------------------------------------------------

    private static LitestreamRegenCoordinator NewSut()
        => new(NullLogger<LitestreamRegenCoordinator>.Instance);

    private static async Task<RegenSignal> ReadSignalAsync(LitestreamRegenCoordinator coord)
    {
        using var cts = new CancellationTokenSource(PollTimeout);
        try
        {
            return await coord.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail($"Expected a regen signal within {PollTimeout.TotalSeconds:F1}s but none arrived.");
            throw; // unreachable
        }
    }

    private static async Task AssertNoMoreSignalsAsync(LitestreamRegenCoordinator coord)
    {
        // Give any pending signal time to land; if the channel is genuinely empty we expect
        // TryRead to return false consistently.
        await Task.Delay(50);
        Assert.False(coord.Reader.TryRead(out _),
            "Expected no further regen signals to be queued.");
    }
}
