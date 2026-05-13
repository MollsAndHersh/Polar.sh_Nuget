using System.Linq.Expressions;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Base class for every tenant-scoped EF Core <see cref="DbContext"/> in the PolarSharp SDK.
/// Applies a composite global query filter that enforces both tenant isolation AND the
/// per-tenant <c>AllowFakeData</c> toggle on every <see cref="ITenantOwned"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// Resolves the current tenant from <see cref="IMultiTenantContextAccessor"/> at DbContext
/// construction time. For HTTP-driven requests, Finbuckle middleware has already populated
/// the context; for background services (<c>IHostedService</c>), the
/// <c>IPolarTenantScopeInitializer</c> shared abstraction must hydrate the scope before any
/// DbContext is resolved from DI.
/// </para>
/// <para>
/// <strong>The composite filter:</strong>
/// <code>e.TenantId == _currentTenantId AND (_allowFakeData OR !e.IsFakeData)</code>
/// </para>
/// <para>
/// <strong>Defense in depth:</strong> the EF Core filter applied here is layer 1 of 5. SQL
/// Server and PostgreSQL providers additionally enforce Row-Level Security policies at the
/// database layer (layer 2) so that a raw-SQL bypass attempt — e.g., via a misconfigured
/// secondary <see cref="DbContext"/> — is still blocked.
/// </para>
/// <para>
/// <strong>AppMasterAdmin bypass:</strong> when (and only when) the request has been
/// explicitly annotated with the <c>[AllowCrossTenant]</c> attribute, the request-scoped
/// flag <see cref="IAppMasterAdminCrossTenantContext.IsAllowedCrossTenantAccess"/> is set to
/// <see langword="true"/>. The filter then short-circuits, granting site-level admins
/// visibility across all tenants — but ONLY on routes that opted in via the attribute. The
/// default request scope remains tenant-bound even for AppMasterAdmin users.
/// </para>
/// </remarks>
public abstract class TenantAwareDbContextBase : DbContext
{
    private readonly string _currentTenantId;
    private readonly bool _allowFakeData;
    private readonly bool _isAppMasterAdminCrossTenant;

    /// <summary>Initializes the DbContext with tenant + fake-data context captured from DI at construction time.</summary>
    /// <param name="options">The DbContext options (provider, connection string, etc.).</param>
    /// <param name="services">The current scope's <see cref="IServiceProvider"/> — used to resolve tenant context.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="IMultiTenantContext"/> is registered or no tenant is resolved.
    /// In background contexts, hydrate the scope via <c>IPolarTenantScopeInitializer</c> before resolving the DbContext.
    /// </exception>
    protected TenantAwareDbContextBase(DbContextOptions options, IServiceProvider services)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(services);

        var accessor = services.GetService<IMultiTenantContextAccessor>()
            ?? throw new InvalidOperationException(
                "No IMultiTenantContextAccessor registered. Ensure AddPolarMultiTenant() was called.");

        var tenant = accessor.MultiTenantContext?.TenantInfo
            ?? throw new InvalidOperationException(
                "No active tenant in scope. For background contexts, hydrate via IPolarTenantScopeInitializer before resolving the DbContext.");

        _currentTenantId = tenant.Id
            ?? throw new InvalidOperationException("Resolved tenant has no Id.");

        // Fake-data and cross-tenant flags are pulled from optional ambient contexts. When the
        // contexts are not registered (e.g., consumer hasn't installed EcommerceStoreManagement
        // or hasn't opted into AppMasterAdmin cross-tenant), they default to safe values.
        _allowFakeData = services.GetService<IFakeDataPolicy>()?.AllowFakeData ?? false;
        _isAppMasterAdminCrossTenant = services.GetService<IAppMasterAdminCrossTenantContext>()?.IsAllowedCrossTenantAccess ?? false;
    }

    /// <summary>The resolved current-tenant ID for this DbContext instance.</summary>
    /// <remarks>
    /// Captured at construction; immutable for the DbContext's lifetime. Used by the global
    /// query filter and by <see cref="SaveChanges"/> to stamp <see cref="ITenantOwned.TenantId"/>
    /// on newly-added entities that omit it.
    /// </remarks>
    protected string CurrentTenantId => _currentTenantId;

    /// <summary>Indicates whether the current tenant has fake-data inclusion enabled.</summary>
    protected bool AllowFakeData => _allowFakeData;

    /// <summary>Indicates whether the current request is an AppMasterAdmin cross-tenant operation.</summary>
    /// <remarks>
    /// <see langword="true"/> only when the route explicitly opted in via the <c>[AllowCrossTenant]</c>
    /// attribute AND the authenticated user has both the <c>IsAppMasterAdmin</c> flag AND the
    /// <c>AppMasterAdmin</c> role claim (dual-flag verification at the user resolution layer).
    /// </remarks>
    protected bool IsAppMasterAdminCrossTenant => _isAppMasterAdminCrossTenant;

    /// <summary>
    /// Applies the composite tenant + fake-data global query filter to every
    /// <see cref="ITenantOwned"/> entity discovered in the model.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <remarks>
    /// Called automatically by EF Core during model creation. Derived contexts that override
    /// <see cref="OnModelCreating"/> must call <c>base.OnModelCreating(modelBuilder)</c>.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        ApplyTenantFiltersToAllOwnedEntities(modelBuilder);
    }

    private void ApplyTenantFiltersToAllOwnedEntities(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                ApplyFilter(modelBuilder, entityType);
            }
        }
    }

    private void ApplyFilter(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        // Build expression: (e) => isAppMasterAdminCrossTenant || (e.TenantId == currentTenantId && (allowFakeData || !e.IsFakeData))
        //
        // CRITICAL: do NOT use Expression.Constant for any of _currentTenantId / _allowFakeData /
        // _isAppMasterAdminCrossTenant — EF Core caches the model per DbContext type, so any
        // constant-folded value gets baked in at first model build and subsequent DbContext
        // instances (with different per-instance values) inherit the stale constant. Cross-tenant
        // isolation then quietly breaks: every tenant sees the first-tenant's filter applied to
        // their queries.
        //
        // Instead we reference the fields through `Expression.Field(Expression.Constant(this), ...)`,
        // which captures `this` as a closure variable. EF Core's expression analyser detects the
        // closure and re-parameterises the query per DbContext instance, so each tenant's filter
        // uses that tenant's _currentTenantId value at query-execution time.
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var tenantIdProp = Expression.Property(parameter, nameof(ITenantOwned.TenantId));
        var isFakeProp = Expression.Property(parameter, nameof(IFakeDataAware.IsFakeData));

        var contextRef = Expression.Constant(this, typeof(TenantAwareDbContextBase));
        var currentTenantIdRef = Expression.Field(contextRef, nameof(_currentTenantId));
        var allowFakeDataRef = Expression.Field(contextRef, nameof(_allowFakeData));
        var crossTenantRef = Expression.Field(contextRef, nameof(_isAppMasterAdminCrossTenant));

        var tenantMatches = Expression.Equal(tenantIdProp, currentTenantIdRef);
        var notFakeOrAllowed = Expression.OrElse(allowFakeDataRef, Expression.Not(isFakeProp));
        var tenantBound = Expression.AndAlso(tenantMatches, notFakeOrAllowed);
        var body = Expression.OrElse(crossTenantRef, tenantBound);

        var lambda = Expression.Lambda(body, parameter);
        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Stamps <see cref="ITenantOwned.TenantId"/> on any newly-added entity that doesn't
    /// already carry one. Prevents accidental cross-tenant writes from caller code that forgot
    /// to set the field.
    /// </remarks>
    public override int SaveChanges()
    {
        StampNewEntities();
        return base.SaveChanges();
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampNewEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampNewEntities()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            if (entry.Entity is not ITenantOwned owned) continue;
            if (!string.IsNullOrEmpty(owned.TenantId)) continue;

            // The TenantId getter is interface-typed; backing field name is convention-bound.
            // EF entity classes typically expose a settable Guid TenantId column; explicit interface
            // implementation routes the read through that column.
            var entityType = entry.Metadata;
            var prop = entityType.FindProperty("TenantId");
            if (prop is not null)
            {
                entry.CurrentValues[prop] = prop.ClrType == typeof(Guid)
                    ? Guid.Parse(_currentTenantId)
                    : (object)_currentTenantId;
            }
        }
    }
}
