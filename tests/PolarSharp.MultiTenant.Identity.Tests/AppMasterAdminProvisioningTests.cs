using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.Tests;

/// <summary>
/// Verifies the provisioning workflow grants and revokes the AppMasterAdmin flag and refuses
/// to demote the only remaining admin (the last-admin guard).
/// </summary>
public sealed class AppMasterAdminProvisioningTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly UserManager<PolarApplicationUser> _userManager;
    private readonly RoleManager<PolarApplicationRole> _roleManager;
    private readonly IAppMasterAdminProvisioning _provisioning;

    public AppMasterAdminProvisioningTests()
    {
        // Keep one in-memory SQLite connection alive for the test class so the schema persists
        // across the per-scope DbContext instances UserManager creates.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddDataProtection();   // required for DefaultTokenProviders' DataProtectorTokenProvider
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
        services.AddScoped<IAppMasterAdminProvisioning, AppMasterAdminProvisioning>();

        _services = services.BuildServiceProvider();
        _userManager = _services.GetRequiredService<UserManager<PolarApplicationUser>>();
        _roleManager = _services.GetRequiredService<RoleManager<PolarApplicationRole>>();
        _provisioning = _services.GetRequiredService<IAppMasterAdminProvisioning>();
    }

    public async Task InitializeAsync()
    {
        var db = _services.GetRequiredService<PolarUserDbContext>();
        await db.Database.EnsureCreatedAsync();

        await _roleManager.CreateAsync(new PolarApplicationRole(PolarRoles.AppMasterAdmin)
        {
            IsBuiltIn = true,
            IsSiteLevel = true,
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GrantAppMasterAdmin_flips_flag_and_adds_role()
    {
        var user = new PolarApplicationUser { UserName = "mollie@example.com", Email = "mollie@example.com" };
        await _userManager.CreateAsync(user, "Password1234");

        var result = await _provisioning.GrantAppMasterAdminAsync(user.Id);
        Assert.True(result.Succeeded);

        var reloaded = await _userManager.FindByIdAsync(user.Id.ToString());
        Assert.True(reloaded!.IsAppMasterAdmin);
        Assert.True(await _userManager.IsInRoleAsync(reloaded, PolarRoles.AppMasterAdmin));
    }

    [Fact]
    public async Task RevokeAppMasterAdmin_refuses_to_demote_the_only_remaining_admin()
    {
        var user = new PolarApplicationUser { UserName = "lone@example.com", Email = "lone@example.com" };
        await _userManager.CreateAsync(user, "Password1234");
        await _provisioning.GrantAppMasterAdminAsync(user.Id);

        var result = await _provisioning.RevokeAppMasterAdminAsync(user.Id);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "LastAppMasterAdmin");

        var reloaded = await _userManager.FindByIdAsync(user.Id.ToString());
        Assert.True(reloaded!.IsAppMasterAdmin);
    }

    [Fact]
    public async Task RevokeAppMasterAdmin_succeeds_when_a_second_admin_exists()
    {
        var first = new PolarApplicationUser { UserName = "first@example.com", Email = "first@example.com" };
        await _userManager.CreateAsync(first, "Password1234");
        await _provisioning.GrantAppMasterAdminAsync(first.Id);

        var second = new PolarApplicationUser { UserName = "second@example.com", Email = "second@example.com" };
        await _userManager.CreateAsync(second, "Password1234");
        await _provisioning.GrantAppMasterAdminAsync(second.Id);

        var result = await _provisioning.RevokeAppMasterAdminAsync(first.Id);
        Assert.True(result.Succeeded);

        var reloaded = await _userManager.FindByIdAsync(first.Id.ToString());
        Assert.False(reloaded!.IsAppMasterAdmin);
        Assert.False(await _userManager.IsInRoleAsync(reloaded, PolarRoles.AppMasterAdmin));
    }

    [Fact]
    public async Task ListAppMasterAdmins_returns_only_users_with_the_flag()
    {
        var admin = new PolarApplicationUser { UserName = "admin@example.com", Email = "admin@example.com" };
        await _userManager.CreateAsync(admin, "Password1234");
        await _provisioning.GrantAppMasterAdminAsync(admin.Id);

        var nonAdmin = new PolarApplicationUser { UserName = "user@example.com", Email = "user@example.com" };
        await _userManager.CreateAsync(nonAdmin, "Password1234");

        var admins = await _provisioning.ListAppMasterAdminsAsync();
        Assert.Single(admins);
        Assert.Equal(admin.Id, admins[0].Id);
    }
}
