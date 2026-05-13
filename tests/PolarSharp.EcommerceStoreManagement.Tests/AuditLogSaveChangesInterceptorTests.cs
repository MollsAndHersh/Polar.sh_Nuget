using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Services;
using PolarSharp.EcommerceStoreManagement.Tests.Infrastructure;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Tests for the v2.0 (TASK-V20-013) <see cref="AuditLogSaveChangesInterceptor"/>. Verifies
/// that every Create / Update / Delete on a tenant-owned entity produces a corresponding
/// <see cref="AdminAuditLogEntry"/> row in the same SaveChanges transaction, with correct
/// before/after snapshots, changed-fields list, fake-data tagging, cross-tenant marker, and
/// recursion guard against the audit log itself.
/// </summary>
public sealed class AuditLogSaveChangesInterceptorTests
{
    private static readonly Guid ActorUserId = new("11111111-1111-1111-1111-111111111111");
    private const string ActorEmail = "ops@example.test";
    private const string CurrentTenant = "tenant-current";

    private sealed class FixedActorProvider(AuditActor actor) : IAuditLogActorProvider
    {
        public AuditActor GetCurrentActor() => actor;
    }

    private static void RegisterAudit(IServiceCollection s, AuditActor? actor = null)
    {
        s.AddSingleton<IAuditLogActorProvider>(new FixedActorProvider(actor ?? new AuditActor(
            ActorUserId, ActorEmail, IsAppMasterAdmin: false, CurrentTenantId: Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"))));
        s.AddScoped<AuditLogSaveChangesInterceptor>();
    }

    [Fact]
    public async Task Create_emits_one_audit_row_with_AfterValues_only_and_ActorIdentity_snapshotted()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: CurrentTenant,
            configureServices: s => RegisterAudit(s));

        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity
            {
                Id = Guid.NewGuid(),
                MasterName = "Test Product",
                MasterDescription = "Description",
            });
            await db.SaveChangesAsync();
        }

        using (var verify = ctx.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var rows = await db.AuditLog.ToListAsync();
            var productAudit = Assert.Single(rows, r => r.EntityType == nameof(LocalProductEntity));
            Assert.Equal(AuditAction.Create, productAudit.Action);
            Assert.Null(productAudit.BeforeValues);
            Assert.NotNull(productAudit.AfterValues);
            Assert.Equal("Test Product", productAudit.AfterValues!["MasterName"]?.GetValue<string>());
            Assert.Equal(ActorUserId, productAudit.ActorUserId);
            Assert.Equal(ActorEmail, productAudit.ActorEmail);
            Assert.Empty(productAudit.ChangedFields);    // ChangedFields is for Update only
        }
    }

    [Fact]
    public async Task Update_emits_one_audit_row_with_BeforeAfter_AND_ChangedFields()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: CurrentTenant,
            configureServices: s => RegisterAudit(s));

        var productId = Guid.NewGuid();
        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity { Id = productId, MasterName = "Original Name", MasterDescription = "v1" });
            await db.SaveChangesAsync();
        }

        // Clear audit log so we only see the update event in the verification step.
        using (var clearScope = ctx.CreateScope())
        {
            var db = clearScope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.AuditLog.RemoveRange(await db.AuditLog.ToListAsync());
            await db.SaveChangesAsync();
        }

        // Now mutate the existing product.
        using (var mutateScope = ctx.CreateScope())
        {
            var db = mutateScope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var product = await db.Products.FirstAsync(p => p.Id == productId);
            product.MasterName = "Renamed Product";
            product.MasterDescription = "v2";
            await db.SaveChangesAsync();
        }

        using (var verify = ctx.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var rows = await db.AuditLog.ToListAsync();
            var updateAudit = Assert.Single(rows, r => r.EntityType == nameof(LocalProductEntity) && r.Action == AuditAction.Update);
            Assert.NotNull(updateAudit.BeforeValues);
            Assert.NotNull(updateAudit.AfterValues);
            Assert.Equal("Original Name", updateAudit.BeforeValues!["MasterName"]?.GetValue<string>());
            Assert.Equal("Renamed Product", updateAudit.AfterValues!["MasterName"]?.GetValue<string>());
            Assert.Contains("MasterName", updateAudit.ChangedFields);
            Assert.Contains("MasterDescription", updateAudit.ChangedFields);
        }
    }

    [Fact]
    public async Task Delete_emits_one_audit_row_with_BeforeValues_only()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: CurrentTenant,
            configureServices: s => RegisterAudit(s));

        var productId = Guid.NewGuid();
        using (var seed = ctx.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity { Id = productId, MasterName = "Doomed", MasterDescription = "x" });
            await db.SaveChangesAsync();
            db.AuditLog.RemoveRange(await db.AuditLog.ToListAsync());
            await db.SaveChangesAsync();
        }

        using (var del = ctx.CreateScope())
        {
            var db = del.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Remove(await db.Products.FirstAsync(p => p.Id == productId));
            await db.SaveChangesAsync();
        }

        using (var verify = ctx.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var deleteAudit = Assert.Single(await db.AuditLog.ToListAsync(), r => r.Action == AuditAction.Delete);
            Assert.Equal(nameof(LocalProductEntity), deleteAudit.EntityType);
            Assert.NotNull(deleteAudit.BeforeValues);
            Assert.Null(deleteAudit.AfterValues);
            Assert.Equal("Doomed", deleteAudit.BeforeValues!["MasterName"]?.GetValue<string>());
        }
    }

    [Fact]
    public async Task AdminAuditLogEntry_writes_are_NOT_recursively_audited()
    {
        // RefundService writes AdminAuditLogEntry rows manually. Without the recursion guard,
        // the interceptor would audit those manual writes, producing infinite cascading rows.
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: CurrentTenant,
            configureServices: s => RegisterAudit(s));

        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.AuditLog.Add(new AdminAuditLogEntry
            {
                Id = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                ActorEmail = "manual@example.test",
                EntityType = "Refund",
                EntityId = Guid.NewGuid(),
                Action = AuditAction.Create,
                OccurredAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using (var verify = ctx.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var rows = await db.AuditLog.ToListAsync();
            // Exactly one row — the manual one. No interceptor-emitted audit-of-the-audit row.
            Assert.Single(rows);
            Assert.Equal("Refund", rows[0].EntityType);
            Assert.Equal("manual@example.test", rows[0].ActorEmail);
        }
    }

    [Fact]
    public async Task IsFakeData_flag_propagates_from_mutated_entity_to_audit_row()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: CurrentTenant,
            configureServices: s => RegisterAudit(s));

        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity
            {
                Id = Guid.NewGuid(),
                MasterName = "Fake Seed Product",
                MasterDescription = "from PolarSharp.DataSeeding",
                IsFakeData = true,
            });
            await db.SaveChangesAsync();
        }

        // The audit row inherits IsFakeData=true, so it falls under the same global filter
        // that hides fake-data rows from operator views by default. Use IgnoreQueryFilters
        // to look at it directly in the test.
        using (var verify = ctx.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var auditWithFakeData = await db.AuditLog
                .IgnoreQueryFilters()
                .Where(r => r.EntityType == nameof(LocalProductEntity))
                .SingleAsync();
            Assert.True(auditWithFakeData.IsFakeData);
        }
    }

    [Fact]
    public async Task CrossTenantAccess_marker_set_when_actor_CurrentTenantId_differs_from_entity_TenantId()
    {
        // Simulate an AppMasterAdmin operating on a different tenant than their current scope.
        var adminInOtherTenant = new AuditActor(
            UserId: Guid.NewGuid(),
            Email: "platform-admin@example.test",
            IsAppMasterAdmin: true,
            CurrentTenantId: Guid.NewGuid());     // some other tenant

        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: CurrentTenant,         // entity will be in this tenant
            configureServices: s => RegisterAudit(s, actor: adminInOtherTenant));

        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity { Id = Guid.NewGuid(), MasterName = "X-tenant op", MasterDescription = "y" });
            await db.SaveChangesAsync();
        }

        using (var verify = ctx.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var auditRow = Assert.Single(await db.AuditLog.ToListAsync(), r => r.EntityType == nameof(LocalProductEntity));
            Assert.True(auditRow.CrossTenantAccess, "CrossTenantAccess should be true when actor.CurrentTenantId != entity.TenantId");
            Assert.Equal("platform-admin@example.test", auditRow.ActorEmail);
        }
    }

    [Fact]
    public async Task Multiple_mutations_in_one_SaveChanges_produce_multiple_audit_rows_in_same_transaction()
    {
        await using var ctx = await CatalogTestContext.CreateAsync(
            initialTenantId: CurrentTenant,
            configureServices: s => RegisterAudit(s));

        using (var scope = ctx.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity { Id = Guid.NewGuid(), MasterName = "Bulk A", MasterDescription = "" });
            db.Products.Add(new LocalProductEntity { Id = Guid.NewGuid(), MasterName = "Bulk B", MasterDescription = "" });
            db.Products.Add(new LocalProductEntity { Id = Guid.NewGuid(), MasterName = "Bulk C", MasterDescription = "" });
            await db.SaveChangesAsync();
        }

        using (var verify = ctx.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var productAudits = await db.AuditLog
                .Where(r => r.EntityType == nameof(LocalProductEntity))
                .ToListAsync();
            Assert.Equal(3, productAudits.Count);
            Assert.All(productAudits, r => Assert.Equal(AuditAction.Create, r.Action));
        }
    }
}
