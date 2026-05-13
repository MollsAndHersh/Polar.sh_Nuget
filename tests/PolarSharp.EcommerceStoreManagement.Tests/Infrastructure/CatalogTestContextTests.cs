using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;

namespace PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;

/// <summary>
/// Smoke tests for <see cref="CatalogTestContext"/> itself. Confirms the in-memory schema is
/// created, the DbContext resolves under the faked Finbuckle context, tenant switching takes
/// effect for the global query filter, and cross-tenant isolation works as production code
/// expects.
/// </summary>
public sealed class CatalogTestContextTests
{
    [Fact]
    public async Task Harness_initialises_schema_and_resolves_DbContext()
    {
        await using var ctx = await CatalogTestContext.CreateAsync();

        using var scope = ctx.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();

        // The schema is in place — Products table is queryable.
        var products = await db.Products.ToListAsync();
        Assert.Empty(products);
        Assert.Equal(CatalogTestContext.DefaultTenantId, ctx.CurrentTenantId);
    }

    [Fact]
    public async Task SetCurrentTenant_changes_tenant_in_scope()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: "tenant-A");
        Assert.Equal("tenant-A", ctx.CurrentTenantId);

        ctx.SetCurrentTenant("tenant-B");
        Assert.Equal("tenant-B", ctx.CurrentTenantId);
    }

    [Fact]
    public async Task Global_tenant_filter_isolates_writes_across_tenants()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(initialTenantId: "tenant-A");

        // Seed a category under tenant-A.
        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Categories.Add(new LocalCategoryEntity
            {
                Id = Guid.NewGuid(),
                MasterName = "Audio",
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Diagnostic: verify the row was actually stamped with tenant-A by reading with filters disabled.
        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var allRows = await db.Categories.IgnoreQueryFilters().ToListAsync();
            Assert.Single(allRows);
            Assert.Equal("tenant-A", allRows[0].TenantId);
        }

        // Switch to tenant-B — global filter must hide tenant-A's row.
        ctx.SetCurrentTenant("tenant-B");
        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var visible = await db.Categories.ToListAsync();
            Assert.Empty(visible);
        }

        // Switch back to tenant-A — the row is visible again.
        ctx.SetCurrentTenant("tenant-A");
        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var visible = await db.Categories.ToListAsync();
            Assert.Single(visible);
            Assert.Equal("Audio", visible[0].MasterName);
        }
    }
}
