using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.Onboarding;

/// <summary>
/// <see cref="IOnboardedTenantSink"/> implementation that writes the onboarded tenant into
/// the EF Core-backed tenant registry from
/// <c>PolarSharp.MultiTenant.EntityFrameworkCore</c>.
/// </summary>
/// <remarks>
/// <para>
/// Translates <see cref="OnboardedTenantResult"/> to <see cref="PolarTenantInfoEntity"/> and
/// inserts via the registered <see cref="PolarTenantDbContext"/>. Cache invalidation is
/// performed by the EF store layer's own change-tracking interceptors (no explicit call
/// needed here — Phase 2 wires the invalidation hooks transparently).
/// </para>
/// </remarks>
public sealed class EfMultiTenantStoreSink : IOnboardedTenantSink
{
    private readonly PolarTenantDbContext _db;

    /// <summary>Initializes the sink with the EF-backed tenant DbContext.</summary>
    public EfMultiTenantStoreSink(PolarTenantDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc/>
    public async Task PersistAsync(OnboardedTenantResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var entity = new PolarTenantInfoEntity
        {
            // PolarTenantBase required init properties:
            Id = result.TenantId,
            Name = result.OrganizationSlug,
            Slug = result.OrganizationSlug,
            CreatedAt = result.OnboardedAt,
            // Entity-specific:
            Identifier = result.OrganizationSlug,
            PolarAccessToken = result.AccessToken,
            Server = result.Server,
            WebhookEndpointId = result.WebhookEndpointId,
            WebhookSecret = result.WebhookSecret,
        };

        _db.Tenants.Add(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
