using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests;

/// <summary>
/// Unit tests for the default <see cref="IPolarTenantScopeInitializer"/> impl that ships in
/// PolarSharp.MultiTenant.EntityFrameworkCore. The two-phase API: ResolveTenantAsync looks
/// up the tenant; the caller applies it via SetCurrentTenant in their own frame.
/// </summary>
public sealed class DefaultPolarTenantScopeInitializerTests
{
    private const string TenantA = "tenant-a";

    [Fact]
    public async Task ResolveTenantAsync_with_known_tenant_returns_that_tenant()
    {
        var sp = BuildHarness(tenantId: TenantA);
        using var scope = sp.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IPolarTenantScopeInitializer>();

        var tenant = await initializer.ResolveTenantAsync(TenantA, CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal(TenantA, tenant.Id);
    }

    [Fact]
    public async Task ResolveTenantAsync_with_unknown_tenant_id_returns_null()
    {
        var sp = BuildHarness(tenantId: TenantA);
        using var scope = sp.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IPolarTenantScopeInitializer>();

        var tenant = await initializer.ResolveTenantAsync("nonexistent-tenant", CancellationToken.None);

        Assert.Null(tenant);
    }

    [Fact]
    public async Task ResolveTenantAsync_with_null_or_empty_tenantId_throws_ArgumentException()
    {
        var sp = BuildHarness(tenantId: TenantA);
        using var scope = sp.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IPolarTenantScopeInitializer>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            initializer.ResolveTenantAsync(string.Empty, CancellationToken.None));
    }

    [Fact]
    public void SetCurrentTenant_assigns_the_tenant_via_IMultiTenantContextSetter()
    {
        var sp = BuildHarness(tenantId: TenantA);
        using var scope = sp.CreateScope();

        var tenant = new PolarTenantInfo { Id = TenantA, Identifier = TenantA, Name = TenantA };
        scope.ServiceProvider.SetCurrentTenant(tenant);

        var nonGen = scope.ServiceProvider.GetRequiredService<IMultiTenantContextAccessor>();
        Assert.NotNull(nonGen.MultiTenantContext);
        Assert.Equal(TenantA, nonGen.MultiTenantContext.TenantInfo?.Id);
    }

    [Fact]
    public void SetCurrentTenant_with_null_args_throws()
    {
        var sp = BuildHarness(tenantId: TenantA);
        using var scope = sp.CreateScope();
        var tenant = new PolarTenantInfo { Id = TenantA, Identifier = TenantA, Name = TenantA };

        Assert.Throws<ArgumentNullException>(() => ((IServiceProvider)null!).SetCurrentTenant(tenant));
        Assert.Throws<ArgumentNullException>(() => scope.ServiceProvider.SetCurrentTenant(null!));
    }

    private static ServiceProvider BuildHarness(string tenantId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NullLogger<DefaultPolarTenantScopeInitializer>.Instance);

        var tenant = new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId };
        var inMemoryStore = new InMemoryStore<PolarTenantInfo>(new[] { tenant });
        services.AddSingleton<IMultiTenantStore<PolarTenantInfo>>(inMemoryStore);

        // Mirror Finbuckle's production registration: one AsyncLocal accessor instance backs
        // the reader interface AND the setter (Singleton, per Finbuckle's AddMultiTenant).
        services.AddSingleton<AsyncLocalMultiTenantContextAccessor<PolarTenantInfo>>();
        services.AddSingleton<IMultiTenantContextAccessor<PolarTenantInfo>>(sp => sp.GetRequiredService<AsyncLocalMultiTenantContextAccessor<PolarTenantInfo>>());
        services.AddSingleton<IMultiTenantContextAccessor>(sp => sp.GetRequiredService<AsyncLocalMultiTenantContextAccessor<PolarTenantInfo>>());
        services.AddSingleton<IMultiTenantContextSetter>(sp => sp.GetRequiredService<AsyncLocalMultiTenantContextAccessor<PolarTenantInfo>>());

        services.AddScoped<IPolarTenantScopeInitializer, DefaultPolarTenantScopeInitializer>();

        return services.BuildServiceProvider();
    }

    /// <summary>Minimal in-memory store for the harness.</summary>
    private sealed class InMemoryStore<T>(IReadOnlyList<T> tenants) : IMultiTenantStore<T> where T : class, ITenantInfo, new()
    {
        public Task<bool> AddAsync(T tenantInfo) => Task.FromResult(true);
        public Task<bool> UpdateAsync(T tenantInfo) => Task.FromResult(true);
        public Task<bool> RemoveAsync(string identifier) => Task.FromResult(true);
        public Task<T?> TryGetAsync(string id) => Task.FromResult(tenants.FirstOrDefault(t => t.Id == id));
        public Task<T?> TryGetByIdentifierAsync(string identifier) => Task.FromResult(tenants.FirstOrDefault(t => t.Identifier == identifier));
        public Task<IEnumerable<T>> GetAllAsync() => Task.FromResult<IEnumerable<T>>(tenants);
        public Task<IEnumerable<T>> GetAllAsync(int take, int skip) => Task.FromResult<IEnumerable<T>>(tenants.Skip(skip).Take(take));
        public Task<T?> GetAsync(string id) => Task.FromResult(tenants.FirstOrDefault(t => t.Id == id));
        public Task<T?> GetByIdentifierAsync(string identifier) => Task.FromResult(tenants.FirstOrDefault(t => t.Identifier == identifier));
    }
}
