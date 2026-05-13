using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Onboarding;
using PolarSharp.Onboarding;

namespace PolarSharp.MultiTenant.Identity.Tests;

/// <summary>
/// Verifies the post-processor creates the user when missing, assigns the TenantAdmin
/// membership, and is idempotent on repeat calls.
/// </summary>
public sealed class TenantAdminAutoProvisioningTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public TenantAdminAutoProvisioningTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddDataProtection();
        services.AddDbContext<PolarUserDbContext>(opts => opts.UseSqlite(_connection));
        services.AddIdentityCore<PolarApplicationUser>(opts =>
            {
                opts.User.RequireUniqueEmail = true;
                opts.Password.RequiredLength = 8;
                opts.Password.RequireDigit = false;
                opts.Password.RequireLowercase = false;
                opts.Password.RequireUppercase = false;
                opts.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<PolarApplicationRole>()
            .AddEntityFrameworkStores<PolarUserDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<TenantAdminAutoProvisioningPostProcessor>();

        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var db = _services.GetRequiredService<PolarUserDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = _services.GetRequiredService<RoleManager<PolarApplicationRole>>();
        await roleManager.CreateAsync(new PolarApplicationRole(PolarRoles.TenantAdmin) { IsBuiltIn = true });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() { _services.Dispose(); _connection.Dispose(); }

    [Fact]
    public async Task Creates_user_and_assigns_TenantAdmin_membership_when_user_does_not_exist()
    {
        var processor = _services.GetRequiredService<TenantAdminAutoProvisioningPostProcessor>();
        var tenantId = Guid.NewGuid();

        await processor.ProcessAsync(new OnboardedTenantResult
        {
            TenantId = tenantId.ToString(),
            OrganizationId = "org_x",
            OrganizationSlug = "x",
            AccessToken = "polar_oat",
            WebhookEndpointId = "whep_x",
            WebhookSecret = "whsec_x",
            Server = PolarServer.Sandbox,
            OnboardedAt = DateTimeOffset.UtcNow,
            InitialAdminEmail = "owner@acme.example.com",
        });

        var userManager = _services.GetRequiredService<UserManager<PolarApplicationUser>>();
        var user = await userManager.FindByEmailAsync("owner@acme.example.com");
        Assert.NotNull(user);

        var db = _services.GetRequiredService<PolarUserDbContext>();
        var membership = await db.Memberships.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.UserId == user.Id && m.TenantId == tenantId);
        Assert.NotNull(membership);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task Idempotent_on_repeat_invocation_for_same_user_and_tenant()
    {
        var processor = _services.GetRequiredService<TenantAdminAutoProvisioningPostProcessor>();
        var tenantId = Guid.NewGuid();
        var result = new OnboardedTenantResult
        {
            TenantId = tenantId.ToString(),
            OrganizationId = "org_x", OrganizationSlug = "x",
            AccessToken = "polar_oat", WebhookEndpointId = "whep_x", WebhookSecret = "whsec_x",
            Server = PolarServer.Sandbox, OnboardedAt = DateTimeOffset.UtcNow,
            InitialAdminEmail = "twice@acme.example.com",
        };

        await processor.ProcessAsync(result);
        await processor.ProcessAsync(result);
        await processor.ProcessAsync(result);

        var db = _services.GetRequiredService<PolarUserDbContext>();
        var memberships = await db.Memberships.IgnoreQueryFilters().Where(m => m.TenantId == tenantId).ToListAsync();
        Assert.Single(memberships);
    }

    [Fact]
    public async Task NoOp_when_InitialAdminEmail_is_null()
    {
        var processor = _services.GetRequiredService<TenantAdminAutoProvisioningPostProcessor>();

        await processor.ProcessAsync(new OnboardedTenantResult
        {
            TenantId = Guid.NewGuid().ToString(),
            OrganizationId = "org_x", OrganizationSlug = "x",
            AccessToken = "polar_oat", WebhookEndpointId = "whep_x", WebhookSecret = "whsec_x",
            Server = PolarServer.Sandbox, OnboardedAt = DateTimeOffset.UtcNow,
            InitialAdminEmail = null,
        });

        var db = _services.GetRequiredService<PolarUserDbContext>();
        Assert.Empty(await db.Memberships.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }
}
