namespace PolarSharp.BaseEntities;

/// <summary>
/// Marks an entity as belonging to a specific Polar.sh organization (merchant tenant).
/// </summary>
/// <remarks>
/// Used for multi-tenant routing of webhooks and API responses — every entity Polar emits
/// carries an <c>organization_id</c> field identifying the owning merchant.
/// </remarks>
public interface IPolarOrganizationScoped
{
    /// <summary>Gets the Polar.sh organization identifier the entity belongs to.</summary>
    /// <value>The <c>organization_id</c> field from Polar's wire format.</value>
    string OrganizationId { get; }
}
