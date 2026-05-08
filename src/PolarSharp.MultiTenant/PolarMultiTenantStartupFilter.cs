using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant;

/// <summary>
/// Validates multi-tenant configuration and emits a startup summary before the application
/// begins accepting traffic.
/// </summary>
/// <remarks>
/// Registered automatically by <c>AddPolarMultiTenant()</c>. Performs the following at startup:
/// <list type="bullet">
///   <item>Warns if no tenants are configured (the factory will throw on every request).</item>
///   <item>Warns if any tenant has an empty <c>PolarAccessToken</c>.</item>
///   <item>Logs an <c>Information</c> summary: strategy, tenant count, and tenant identifiers.</item>
/// </list>
/// </remarks>
internal sealed class PolarMultiTenantStartupFilter(
    IOptions<PolarMultiTenantOptions> options,
    ILogger<PolarMultiTenantStartupFilter> logger) : IStartupFilter
{
    /// <summary>
    /// Returns the application builder delegate unchanged after running startup validation.
    /// </summary>
    /// <param name="next">The next startup filter in the pipeline.</param>
    /// <returns>A delegate that validates configuration and then calls <paramref name="next"/>.</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        Validate();

        return next;
    }

    private void Validate()
    {
        var opts = options.Value;

        if (opts.Tenants.Count == 0)
        {
            logger.LogWarning(
                "PolarSharp MultiTenant: No tenants configured. " +
                "Add tenants via PolarSharp:MultiTenant:Tenants in appsettings.json " +
                "or via the configure delegate passed to AddPolarMultiTenant().");
            return;
        }

        var tenantsWithoutToken = opts.Tenants
            .Where(t => string.IsNullOrWhiteSpace(t.PolarAccessToken))
            .Select(t => t.Identifier)
            .ToList();

        if (tenantsWithoutToken.Count > 0)
        {
            logger.LogWarning(
                "PolarSharp MultiTenant: {Count} tenant(s) have no PolarAccessToken configured: {Tenants}. " +
                "API calls for these tenants will fail with authentication errors.",
                tenantsWithoutToken.Count,
                string.Join(", ", tenantsWithoutToken));
        }

        logger.LogInformation(
            "PolarSharp MultiTenant configured: strategy={Strategy}, tenants={TenantCount} ({Identifiers})",
            opts.Strategy,
            opts.Tenants.Count,
            string.Join(", ", opts.Tenants.Select(t => t.Identifier)));
    }
}
