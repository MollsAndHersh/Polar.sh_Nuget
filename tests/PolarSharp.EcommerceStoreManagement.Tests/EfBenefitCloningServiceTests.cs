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
/// Smoke tests for <c>EfBenefitCloningService</c>. Verifies name auto-suffix, Polar-side
/// state reset, and cross-tenant isolation.
/// </summary>
public sealed class EfBenefitCloningServiceTests
{
    [Fact]
    public async Task Clone_auto_suffixes_name_on_collision()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedBenefitAsync(ctx, name: "Premium License");

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Premium License (Copy)", result.ValueOrThrow().Name);
    }

    [Fact]
    public async Task Clone_resets_Polar_side_state_to_fresh_Draft()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedBenefitAsync(
            ctx,
            name: "Premium License",
            polarBenefitId: "polar_ben_abc",
            status: PublishStatus.Published,
            lastPublishedAt: DateTimeOffset.UtcNow);

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        var cloned = result.ValueOrThrow();
        Assert.Equal(PublishStatus.Draft, cloned.Status);
        Assert.Null(cloned.PolarBenefitId);
        Assert.Null(cloned.LastPublishedAt);
    }

    [Fact]
    public async Task Clone_cross_tenant_returns_SourceNotFound()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: "tenant-A",
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedBenefitAsync(ctx, name: "Premium License");

        ctx.SetCurrentTenant("tenant-B");
        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsFailure);
        Assert.Equal(CloningErrorKind.SourceNotFound, result.ErrorOrThrow().Kind);
    }

    private static async Task<BenefitId> SeedBenefitAsync(
        CatalogTestContext ctx,
        string name,
        string? polarBenefitId = null,
        PublishStatus status = PublishStatus.Draft,
        DateTimeOffset? lastPublishedAt = null)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.Benefits.Add(new LocalBenefitEntity
        {
            Id = id,
            BenefitKind = "Custom",
            Name = name,
            Description = $"{name} description",
            PropertiesJson = "{}",
            PolarBenefitId = polarBenefitId,
            Status = status,
            LastPublishedAt = lastPublishedAt,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return new BenefitId(id);
    }

    private static async Task<Result<LocalBenefit, CloningError>> CloneAsync(
        CatalogTestContext ctx,
        BenefitId source,
        CloneBenefitOverrides? overrides = null,
        CloneBenefitOptions? options = null)
    {
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IBenefitCloningService>();
        return await svc.CloneAsync(source, overrides, options);
    }
}
