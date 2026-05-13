using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Cloning;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;
using static PolarSharp.EcommerceStoreManagement.Tests.Infrastructure.ResultTestExtensions;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Smoke tests for <c>EfCheckoutLinkCloningService</c>. Verifies name auto-suffix,
/// Polar-side state reset, and cross-tenant isolation.
/// </summary>
public sealed class EfCheckoutLinkCloningServiceTests
{
    [Fact]
    public async Task Clone_auto_suffixes_name_on_collision()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedCheckoutLinkAsync(ctx, name: "Holiday Sale Link");

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Holiday Sale Link (Copy)", result.ValueOrThrow().Name);
    }

    [Fact]
    public async Task Clone_resets_Polar_side_state_to_fresh_Draft()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedCheckoutLinkAsync(
            ctx,
            name: "Holiday Sale Link",
            polarCheckoutLinkId: "polar_chk_abc",
            status: PublishStatus.Published);

        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PublishStatus.Draft, result.ValueOrThrow().Status);
    }

    [Fact]
    public async Task Clone_cross_tenant_returns_SourceNotFound()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: "tenant-A",
            configureServices: s => s.AddPolarCatalogCloning());

        var sourceId = await SeedCheckoutLinkAsync(ctx, name: "Holiday Sale Link");

        ctx.SetCurrentTenant("tenant-B");
        var result = await CloneAsync(ctx, sourceId);

        Assert.True(result.IsFailure);
        Assert.Equal(CloningErrorKind.SourceNotFound, result.ErrorOrThrow().Kind);
    }

    private static async Task<CheckoutLinkId> SeedCheckoutLinkAsync(
        CatalogTestContext ctx,
        string name,
        string? polarCheckoutLinkId = null,
        PublishStatus status = PublishStatus.Draft)
    {
        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        var id = Guid.NewGuid();
        db.CheckoutLinks.Add(new LocalCheckoutLinkEntity
        {
            Id = id,
            Name = name,
            ProductIdsJson = "[]",
            CustomFieldsJson = "[]",
            AllowDiscountCodes = true,
            PolarCheckoutLinkId = polarCheckoutLinkId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return new CheckoutLinkId(id);
    }

    private static async Task<Result<LocalCheckoutLinkConfig, CloningError>> CloneAsync(
        CatalogTestContext ctx,
        CheckoutLinkId source,
        CloneCheckoutLinkOverrides? overrides = null,
        CloneCheckoutLinkOptions? options = null)
    {
        using var scope = ctx.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICheckoutLinkCloningService>();
        return await svc.CloneAsync(source, overrides, options);
    }
}
