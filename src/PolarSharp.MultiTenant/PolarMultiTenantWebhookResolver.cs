using PolarSharp.Webhooks;

namespace PolarSharp.MultiTenant;

/// <summary>
/// Resolves per-tenant Polar HMAC secrets from the <see cref="PolarMultiTenantOptions.Tenants"/>
/// list by matching a Polar organization ID to the registered <see cref="PolarTenantInfo"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as the <see cref="IWebhookTenantResolver"/> implementation when
/// <c>AddPolarMultiTenant()</c> is called. The <see cref="WebhookValidator"/> calls
/// <see cref="Resolve"/> with the <c>organization_id</c> extracted from the raw
/// (unverified) request body to determine which HMAC secret to use.
/// </para>
/// <para>
/// The lookup table is built once at startup from the tenant list in options and held in
/// a read-only dictionary — no locking required at call time.
/// </para>
/// </remarks>
internal sealed class PolarMultiTenantWebhookResolver : IWebhookTenantResolver
{
    // Key = PolarOrganizationId, Value = (Secrets, TenantId)
    private readonly IReadOnlyDictionary<string, WebhookTenantResolution> _index;

    /// <summary>
    /// Initializes the resolver by indexing all tenants that have a
    /// <see cref="PolarTenantInfo.PolarOrganizationId"/> configured.
    /// </summary>
    /// <param name="opts">The multi-tenant options containing the registered tenant list.</param>
    public PolarMultiTenantWebhookResolver(PolarMultiTenantOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        var index = new Dictionary<string, WebhookTenantResolution>(StringComparer.Ordinal);
        foreach (var tenant in opts.Tenants)
        {
            if (string.IsNullOrEmpty(tenant.PolarOrganizationId))
                continue;

            var secrets = BuildSecretList(tenant);
            if (secrets.Count == 0)
                continue;

            index[tenant.PolarOrganizationId] = new WebhookTenantResolution(secrets, tenant.Id);
        }

        _index = index;
    }

    /// <inheritdoc/>
    public WebhookTenantResolution? Resolve(string polarOrganizationId)
    {
        ArgumentNullException.ThrowIfNull(polarOrganizationId);
        return _index.TryGetValue(polarOrganizationId, out var resolution) ? resolution : null;
    }

    private static IReadOnlyList<string> BuildSecretList(PolarTenantInfo tenant)
    {
        // Support a single per-tenant secret. Future: allow a list for rotation.
        return string.IsNullOrEmpty(tenant.WebhookSecret)
            ? []
            : [tenant.WebhookSecret];
    }
}
