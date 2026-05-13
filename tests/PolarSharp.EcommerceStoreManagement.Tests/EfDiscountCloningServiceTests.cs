using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Retroactive coverage for <c>EfDiscountCloningService</c>. The critical guarantee is the
/// coupon-code-null-on-clone behavior — without it, cloning a code-based discount would
/// trip the <c>(tenant_id, code)</c> unique index. The cloning service defaults the clone's
/// <see cref="LocalDiscount.Code"/> to <see langword="null"/> (which falls outside the
/// partial unique index) unless the caller explicitly supplies a new code.
/// </summary>
public sealed class EfDiscountCloningServiceTests
{
    [Fact]
    public async Task Clone_defaults_Code_to_null_to_avoid_unique_index_violation()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedDiscountAsync(ctx, name: "Summer Sale", code: "SUMMER25");

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ValueOrThrow().Code);                 // The clone becomes an automatic discount
        Assert.Equal("Summer Sale (Copy)", result.ValueOrThrow().MasterName);
    }

    [Fact]
    public async Task Clone_with_explicit_new_code_uses_that_code()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedDiscountAsync(ctx, name: "Summer Sale", code: "SUMMER25");

        var result = await CloneAsync(ctx, sourceId, overrides: new CloneDiscountOverrides
        {
            NewCode = "WINTER25",
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("WINTER25", result.ValueOrThrow().Code);
    }

    [Fact]
    public async Task Clone_with_explicit_new_code_that_collides_returns_OverrideConflict()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedDiscountAsync(ctx, name: "Summer Sale", code: "SUMMER25");
        await SeedDiscountAsync(ctx, name: "Other Sale", code: "TAKEN");

        var result = await CloneAsync(ctx, sourceId, overrides: new CloneDiscountOverrides
        {
            NewCode = "TAKEN",
        });

        Assert.True(result.IsFailure);
        Assert.Equal(CloningErrorKind.OverrideConflictsWithExistingRow, result.ErrorOrThrow().Kind);
    }

    [Fact]
    public async Task Clone_resets_Polar_side_state_to_fresh_Draft()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedDiscountAsync(
            ctx,
            name: "Summer Sale",
            code: "SUMMER25",
            polarDiscountId: "polar_disc_abc",
            status: PublishStatus.Published,
            lastPublishedAt: DateTimeOffset.UtcNow);

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        var cloned = result.ValueOrThrow();
        Assert.Equal(PublishStatus.Draft, cloned.Status);

        // Verify Polar-side state is reset on the entity row too.
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var entity = await db.Discounts.FirstAsync(d => d.Id == Guid.Parse(cloned.Id));
        Assert.Null(entity.PolarDiscountId);
        Assert.Null(entity.LastPublishedAt);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<DiscountId> SeedDiscountAsync(
        CatalogTestContext ctx,
        string name,
        string? code = null,
        string? polarDiscountId = null,
        PublishStatus status = PublishStatus.Draft,
        DateTimeOffset? lastPublishedAt = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Discounts.Add(new LocalDiscountEntity
        {
            Id = id,
            MasterName = name,
            Name = name,
            Code = code,
            Kind = DiscountKind.Percentage,
            Type = "percentage",
            PercentageOff = 25m,
            DurationKind = DiscountDuration.Once,
            DurationWire = "once",
            ApplicableProductIdsJson = "[]",
            PolarDiscountId = polarDiscountId,
            Status = status,
            LastPublishedAt = lastPublishedAt,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return new DiscountId(id);
    }

    private static async Task<Result<LocalDiscount, CloningError>> CloneAsync(
        CatalogTestContext ctx,
        DiscountId source,
        CloneDiscountOverrides? overrides = null,
        CloneDiscountOptions? options = null)
    {
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IDiscountCloningService>();
        return await svc.CloneAsync(source, overrides, options);
    }
}
