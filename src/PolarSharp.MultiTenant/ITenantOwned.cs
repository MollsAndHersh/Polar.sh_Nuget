namespace PolarSharp.MultiTenant;

/// <summary>
/// Marks an entity as belonging to a specific tenant for the multi-tenant EF Core
/// global query filter and Row-Level Security policies.
/// </summary>
/// <remarks>
/// <para>
/// Every entity that should be isolated per-tenant must implement this interface.
/// PolarSharp's <c>TenantAwareDbContextBase</c> applies a composite query filter
/// (<c>e.TenantId == _currentTenantId AND (_allowFakeData OR !e.IsFakeData)</c>) to every
/// derived entity automatically. Combined with database-level RLS policies (SQL Server /
/// PostgreSQL) and physical file isolation (SQLite), this provides defense-in-depth
/// cross-tenant isolation.
/// </para>
/// <para>
/// The interface composes <see cref="IFakeDataAware"/> so every tenant-scoped entity also
/// participates in the fake-data toggle. Set <see cref="IFakeDataAware.IsFakeData"/> to
/// <see langword="false"/> by default on real entities; <c>PolarSharp.DataSeeding</c> sets it
/// to <see langword="true"/> on seeded rows.
/// </para>
/// <para>
/// <strong>TenantId format:</strong> the property type is <c>string</c> to match Finbuckle's
/// <see cref="Finbuckle.MultiTenant.Abstractions.ITenantInfo.Id"/> contract. The recommended
/// convention is to store a <see cref="System.Guid"/> in its canonical string form
/// (e.g., <c>"3fa85f64-5717-4562-b3fc-2c963f66afa6"</c>). EF entity classes typically expose
/// a <c>Guid TenantId</c> column and implement this interface via explicit interface
/// implementation: <c>string ITenantOwned.TenantId =&gt; TenantId.ToString();</c>
/// </para>
/// </remarks>
public interface ITenantOwned : IFakeDataAware
{
    /// <summary>
    /// Gets the tenant identifier the entity belongs to (Finbuckle's
    /// <see cref="Finbuckle.MultiTenant.Abstractions.ITenantInfo.Id"/> as a string).
    /// </summary>
    /// <value>
    /// The owning tenant's identifier. Conventionally a <see cref="System.Guid"/> in canonical
    /// string form. Never <see langword="null"/> or empty for a persisted entity.
    /// </value>
    string TenantId { get; }
}
