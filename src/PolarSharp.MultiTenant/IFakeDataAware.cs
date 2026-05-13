namespace PolarSharp.MultiTenant;

/// <summary>
/// Marks an entity as participating in PolarSharp's fake-data toggle system.
/// </summary>
/// <remarks>
/// <para>
/// Combined with the per-tenant <c>TenantBusinessProfile.AllowFakeData</c> flag, entities
/// flagged <see cref="IsFakeData"/> = <see langword="true"/> are invisible to every read path
/// (queries, reports, publish operations) whenever the tenant has disabled fake-data inclusion.
/// </para>
/// <para>
/// Used by <c>PolarSharp.DataSeeding</c> to tag bulk-generated rows so they can be later
/// excluded or removed without touching production data.
/// </para>
/// <para>
/// <strong>Composition:</strong> the <see cref="ITenantOwned"/> interface extends this one,
/// so every tenant-scoped entity automatically participates in the fake-data filter.
/// </para>
/// </remarks>
public interface IFakeDataAware
{
    /// <summary>
    /// Gets a value indicating whether the entity is fake (seeded) data.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when the entity was created by <c>PolarSharp.DataSeeding</c>;
    /// <see langword="false"/> for real production data (the default).
    /// </value>
    bool IsFakeData { get; }
}
