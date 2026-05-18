using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// Resolves the default tenant info during a single-tenant -> multi-tenant upgrade when
/// <see cref="SingleTenantUpgradeOptions.DefaultTenantStrategy"/> is set to
/// <see cref="DefaultTenantStrategy.HostSupplied"/>.
/// </summary>
/// <remarks>
/// <para>
/// Hosts register an implementation via DI before <see cref="SingleTenantUpgradeServiceCollectionExtensions.AddPolarSingleTenantUpgrade"/>
/// is called. When the strategy is <see cref="DefaultTenantStrategy.HostSupplied"/> and no
/// implementation is registered, the orchestrator fails fast at startup with a clear error.
/// </para>
/// <para>
/// The returned <see cref="PolarTenantInfo"/> must carry a well-formed
/// <see cref="PolarTenantInfo.Id"/> (GUID string) and
/// <see cref="PolarTenantInfo.Identifier"/>. The orchestrator passes it through to both the
/// tenant registry (via <see cref="ITenantRegistryUpgrader"/>) and the per-provider migrator.
/// </para>
/// </remarks>
public interface IDefaultTenantResolver
{
    /// <summary>
    /// Resolves the tenant that existing single-tenant rows will be assigned to.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The default tenant. Implementations must not return <see langword="null"/>.</returns>
    Task<PolarTenantInfo> ResolveAsync(CancellationToken ct);
}
