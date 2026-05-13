using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v1.3.D <see cref="InventoryUpdater"/>. Verifies zero-boundary detection,
/// event publication only on boundary crossings, error mapping for variants that don't
/// track inventory, and bulk-update transactional semantics.
/// </summary>
public sealed class InventoryUpdaterTests
{
    private static void Configure(IServiceCollection s, RecordingNotifier notifier)
    {
        s.AddSingleton<IInventoryEventNotifier>(notifier);
        s.AddScoped<IInventoryUpdater, InventoryUpdater>();
    }

    [Fact]
    public async Task UpdateAsync_routine_decrement_does_not_cross_boundary_or_publish_event()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));
        var variantId = await SeedVariantAsync(ctx, initialCount: 10);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateAsync(variantId, newCount: 9);

        var outcome = result.ValueOrThrow();
        Assert.Equal(10, outcome.OldCount);
        Assert.Equal(9, outcome.NewCount);
        Assert.False(outcome.CrossedZeroBoundary);
        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task UpdateAsync_in_stock_to_zero_crosses_boundary_and_publishes_NowOutOfStock()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));
        var variantId = await SeedVariantAsync(ctx, initialCount: 3);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateAsync(variantId, newCount: 0);

        Assert.True(result.ValueOrThrow().CrossedZeroBoundary);
        Assert.Single(notifier.Events);
        Assert.True(notifier.Events[0].NowOutOfStock);
        Assert.False(notifier.Events[0].BackInStock);
    }

    [Fact]
    public async Task UpdateAsync_zero_to_in_stock_crosses_boundary_and_publishes_BackInStock()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));
        var variantId = await SeedVariantAsync(ctx, initialCount: 0);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateAsync(variantId, newCount: 5);

        Assert.True(result.ValueOrThrow().CrossedZeroBoundary);
        Assert.Single(notifier.Events);
        Assert.False(notifier.Events[0].NowOutOfStock);
        Assert.True(notifier.Events[0].BackInStock);
    }

    [Fact]
    public async Task UpdateAsync_negative_count_returns_InvalidCount()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));
        var variantId = await SeedVariantAsync(ctx, initialCount: 10);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateAsync(variantId, newCount: -1);

        Assert.Equal(InventoryErrorKind.InvalidCount, result.ErrorOrThrow().Kind);
        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task UpdateAsync_variant_with_tracking_disabled_returns_InventoryNotTracked()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));
        var variantId = await SeedVariantAsync(ctx, initialCount: null);   // tracking disabled

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateAsync(variantId, newCount: 5);

        Assert.Equal(InventoryErrorKind.InventoryNotTracked, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task UpdateAsync_missing_variant_returns_VariantNotFound()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateAsync(new VariantId(Guid.NewGuid()), newCount: 5);

        Assert.Equal(InventoryErrorKind.VariantNotFound, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task UpdateManyAsync_persists_all_updates_and_publishes_only_for_boundary_crossings()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));
        var v1 = await SeedVariantAsync(ctx, initialCount: 10);
        var v2 = await SeedVariantAsync(ctx, initialCount: 5);
        var v3 = await SeedVariantAsync(ctx, initialCount: 0);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateManyAsync(
        [
            new InventoryUpdate(v1, NewCount: 9),       // routine decrement; no event
            new InventoryUpdate(v2, NewCount: 0),       // crosses to out-of-stock; event
            new InventoryUpdate(v3, NewCount: 8),       // crosses back in stock; event
        ]);

        var outcomes = result.ValueOrThrow();
        Assert.Equal(3, outcomes.Count);
        Assert.Equal(2, notifier.Events.Count);            // only the two zero-boundary crossings emitted

        // Verify the entities actually got persisted.
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var v1Entity = await db.Variants.FirstAsync(v => v.Id == v1.Value);
        Assert.Equal(9, v1Entity.InventoryCount);
    }

    [Fact]
    public async Task UpdateManyAsync_one_bad_entry_short_circuits_the_whole_batch()
    {
        var notifier = new RecordingNotifier();
        await using var ctx = await CatalogTestContext.CreateAsync(configureServices: s => Configure(s, notifier));
        var v1 = await SeedVariantAsync(ctx, initialCount: 10);

        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryUpdater>();
        var result = await svc.UpdateManyAsync(
        [
            new InventoryUpdate(v1, NewCount: 5),
            new InventoryUpdate(new VariantId(Guid.NewGuid()), NewCount: 5),    // missing variant
        ]);

        Assert.Equal(InventoryErrorKind.VariantNotFound, result.ErrorOrThrow().Kind);
        Assert.Empty(notifier.Events);

        // No persistence happened.
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var v1Entity = await db.Variants.AsNoTracking().FirstAsync(v => v.Id == v1.Value);
        Assert.Equal(10, v1Entity.InventoryCount);
    }

    private static async Task<VariantId> SeedVariantAsync(CatalogTestContext ctx, int? initialCount)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Variants.Add(new LocalProductVariantEntity
        {
            Id = id,
            ProductId = Guid.NewGuid(),
            AxesJson = """{"size":"M"}""",
            IsActive = true,
            InventoryCount = initialCount,
        });
        await db.SaveChangesAsync();
        return new VariantId(id);
    }

    private sealed class RecordingNotifier : IInventoryEventNotifier
    {
        public List<SkuStockChanged> Events { get; } = [];
        public bool TryNotify(SkuStockChanged change) { Events.Add(change); return true; }
    }
}
