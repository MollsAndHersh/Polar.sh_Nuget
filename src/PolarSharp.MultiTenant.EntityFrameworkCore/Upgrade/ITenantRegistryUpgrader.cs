using Finbuckle.MultiTenant.Abstractions;
using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// Inserts (or recognises an existing) default tenant in the tenant registry as part of the
/// single-tenant -> multi-tenant upgrade. Used by every
/// <see cref="ISingleTenantUpgradeMigrator"/> implementation so each provider does not need
/// to know how the registry itself is stored.
/// </summary>
/// <remarks>
/// <para>
/// The abstraction is provider-agnostic by design: a SQLite migrator and a Cosmos migrator
/// both call the same <see cref="UpsertAsync(PolarTenantInfo, CancellationToken)"/> method,
/// and the registered <see cref="IMultiTenantStore{TTenantInfo}"/> handles the persistence.
/// </para>
/// <para>
/// <strong>Idempotent.</strong> <see cref="UpsertAsync(PolarTenantInfo, CancellationToken)"/>
/// returns the existing tenant when a row with the same
/// <see cref="PolarTenantInfo.Identifier"/> is already present — the second invocation of
/// the upgrade therefore behaves exactly like the first.
/// </para>
/// </remarks>
public interface ITenantRegistryUpgrader
{
    /// <summary>
    /// Indicates whether a tenant with the supplied slug already exists in the registry.
    /// </summary>
    /// <param name="slug">The tenant slug (Finbuckle <c>Identifier</c>) to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a matching tenant exists; otherwise <see langword="false"/>.</returns>
    Task<bool> TenantExistsAsync(string slug, CancellationToken ct);

    /// <summary>
    /// Inserts the supplied tenant into the registry. Idempotent: returns the existing
    /// tenant when a row with the same <see cref="PolarTenantInfo.Identifier"/> is already
    /// present.
    /// </summary>
    /// <param name="tenant">The tenant to insert.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted tenant — either the freshly inserted row or the pre-existing match.</returns>
    Task<PolarTenantInfo> UpsertAsync(PolarTenantInfo tenant, CancellationToken ct);
}

/// <summary>
/// Default <see cref="ITenantRegistryUpgrader"/> implementation backed by the registered
/// <see cref="IMultiTenantStore{TTenantInfo}"/>. Works against every provider that ships an
/// EF Core-backed tenant store.
/// </summary>
internal sealed class DefaultTenantRegistryUpgrader : ITenantRegistryUpgrader
{
    private readonly IMultiTenantStore<PolarTenantInfo> _store;

    /// <summary>Initializes a new <see cref="DefaultTenantRegistryUpgrader"/>.</summary>
    /// <param name="store">The registered Finbuckle tenant store.</param>
    public DefaultTenantRegistryUpgrader(IMultiTenantStore<PolarTenantInfo> store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<bool> TenantExistsAsync(string slug, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(slug);
        ct.ThrowIfCancellationRequested();

        var existing = await _store.GetByIdentifierAsync(slug).ConfigureAwait(false);
        return existing is not null;
    }

    /// <inheritdoc/>
    public async Task<PolarTenantInfo> UpsertAsync(PolarTenantInfo tenant, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (string.IsNullOrEmpty(tenant.Id))
        {
            throw new ArgumentException("Tenant Id must be set before upsert.", nameof(tenant));
        }
        if (string.IsNullOrEmpty(tenant.Identifier))
        {
            throw new ArgumentException("Tenant Identifier (slug) must be set before upsert.", nameof(tenant));
        }
        ct.ThrowIfCancellationRequested();

        // Resolve-by-identifier first so two parallel boots converge on the same row rather
        // than racing two inserts that would both fail with a unique-constraint violation.
        var existing = await _store.GetByIdentifierAsync(tenant.Identifier).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var added = await _store.AddAsync(tenant).ConfigureAwait(false);
        if (added)
        {
            return tenant;
        }

        // Lost the race — another boot inserted the same identifier concurrently. Re-resolve
        // and return whatever the registry now holds; the migrator treats this as success.
        var raced = await _store.GetByIdentifierAsync(tenant.Identifier).ConfigureAwait(false);
        return raced
            ?? throw new InvalidOperationException(
                $"Failed to upsert tenant '{tenant.Identifier}' into the registry: " +
                "the store rejected the insert and no matching row could be re-resolved.");
    }
}
