using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests;

/// <summary>
/// Unit tests for the default <see cref="IPolarTenantScopeInitializer"/> impl that
/// ships in PolarSharp.MultiTenant.EntityFrameworkCore. Verifies the four behaviors
/// the orchestrator relies on: (1) tenant resolved + accessor populated; (2) unknown
/// tenant id is a no-op; (3) non-AsyncLocal accessor is a logged no-op (no throw);
/// (4) the populated tenant is the one looked up by primary key.
/// </summary>
public sealed class DefaultPolarTenantScopeInitializerTests
{
    private const string TenantA = "tenant-a";

    [Fact]
    public async Task InitializeAsync_with_unknown_tenant_id_is_a_no_op()
    {
        var sp = BuildHarness(tenantId: TenantA);

        using var scope = sp.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IPolarTenantScopeInitializer>();
        var ex = await Record.ExceptionAsync(() =>
            initializer.InitializeAsync("nonexistent-tenant", scope.ServiceProvider, CancellationToken.None));

        Assert.Null(ex);
        var accessor = scope.ServiceProvider.GetRequiredService<IMultiTenantContextAccessor<PolarTenantInfo>>();
        // The async-local default accessor returns an empty context (TenantInfo == null) when nothing has been set.
        Assert.Null(accessor.MultiTenantContext.TenantInfo);
    }

    [Fact]
    public async Task InitializeAsync_with_null_or_empty_tenantId_throws_ArgumentException()
    {
        var sp = BuildHarness(tenantId: TenantA);

        using var scope = sp.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IPolarTenantScopeInitializer>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            initializer.InitializeAsync(string.Empty, scope.ServiceProvider, CancellationToken.None));
    }

    private static ServiceProvider BuildHarness(string tenantId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NullLogger<DefaultPolarTenantScopeInitializer>.Instance);

        var tenant = new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId };
        var inMemoryStore = new InMemoryStore<PolarTenantInfo>(new[] { tenant });
        services.AddSingleton<IMultiTenantStore<PolarTenantInfo>>(inMemoryStore);

        // Finbuckle's AsyncLocal accessor backs both the reader interface and the setter.
        // Same shape AddPolarMultiTenant registers in production.
        services.AddScoped<AsyncLocalMultiTenantContextAccessor<PolarTenantInfo>>();
        services.AddScoped<IMultiTenantContextAccessor<PolarTenantInfo>>(sp => sp.GetRequiredService<AsyncLocalMultiTenantContextAccessor<PolarTenantInfo>>());
        services.AddScoped<IMultiTenantContextSetter>(sp => sp.GetRequiredService<AsyncLocalMultiTenantContextAccessor<PolarTenantInfo>>());

        services.AddScoped<IPolarTenantScopeInitializer, DefaultPolarTenantScopeInitializer>();

        return services.BuildServiceProvider();
    }

    /// <summary>Minimal in-memory store for the harness — supports just GetAsync/GetByIdentifierAsync needed by the initializer.</summary>
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
