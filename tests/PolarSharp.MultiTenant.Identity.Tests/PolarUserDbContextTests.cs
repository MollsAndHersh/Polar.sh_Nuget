using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.Tests;

/// <summary>
/// Verifies the DbContext schema, key constraints, and basic CRUD against a SQLite database
/// held open for the lifetime of the test instance (so the in-memory store persists across
/// the multiple DbContext instances each test creates).
/// </summary>
public sealed class PolarUserDbContextTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PolarUserDbContext> _options;

    public PolarUserDbContextTests()
    {
        // Open a connection and KEEP IT OPEN so the in-memory database stays alive across
        // the per-test DbContext instances. Without this, SQLite drops the schema as soon as
        // the last connection closes.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<PolarUserDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public async Task InitializeAsync()
    {
        await using var db = new PolarUserDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Schema_creates_membership_and_platform_audit_tables()
    {
        await using var db = new PolarUserDbContext(_options);
        Assert.NotNull(await db.Memberships.IgnoreQueryFilters().ToListAsync());
        Assert.NotNull(await db.PlatformAuditLog.ToListAsync());
    }

    [Fact]
    public async Task Membership_unique_index_prevents_duplicate_user_role_assignment()
    {
        await using var db = new PolarUserDbContext(_options);

        // Seed a real user + role first so the FK on the membership row resolves.
        var user = new PolarApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "alice@example.com",
            NormalizedUserName = "ALICE@EXAMPLE.COM",
            Email = "alice@example.com",
            NormalizedEmail = "ALICE@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        var role = new PolarApplicationRole(PolarRoles.TenantAdmin)
        {
            Id = Guid.NewGuid(),
            NormalizedName = PolarRoles.TenantAdmin.ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            IsBuiltIn = true,
        };
        db.Users.Add(user);
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var tenantId = Guid.NewGuid();

        db.Memberships.Add(new PolarUserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            RoleId = role.Id,
            JoinedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.Memberships.Add(new PolarUserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            RoleId = role.Id,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PlatformAuditLogEntry_persists_cross_tenant_marker_and_justification()
    {
        await using var db = new PolarUserDbContext(_options);

        var entry = new PlatformAuditLogEntry
        {
            Id = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            ActorEmail = "ops@example.com",
            TargetTenantId = Guid.NewGuid(),
            EntityType = "LocalProduct",
            EntityId = Guid.NewGuid(),
            Action = PlatformAuditAction.Update,
            OccurredAt = DateTimeOffset.UtcNow,
            CrossTenantAccess = true,
            JustificationKind = PlatformAuditJustificationKind.SupportTicket,
            JustificationText = "Investigating ZD-12345 — refund preview produced wrong total.",
        };
        db.PlatformAuditLog.Add(entry);
        await db.SaveChangesAsync();

        var readBack = await db.PlatformAuditLog.SingleAsync(p => p.Id == entry.Id);
        Assert.True(readBack.CrossTenantAccess);
        Assert.Equal(PlatformAuditJustificationKind.SupportTicket, readBack.JustificationKind);
        Assert.Equal(PlatformAuditAction.Update, readBack.Action);
        Assert.Contains("ZD-12345", readBack.JustificationText);
    }
}
