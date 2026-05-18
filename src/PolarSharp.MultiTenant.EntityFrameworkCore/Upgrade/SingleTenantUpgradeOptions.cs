namespace PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

/// <summary>
/// Configuration for the single-tenant -> multi-tenant data upgrade flow.
/// Bound from the appsettings section <c>PolarSharp:MultiTenant:SingleTenantUpgrade</c>.
/// </summary>
/// <remarks>
/// <para>
/// The upgrade is the one-time backfill that runs the first time a host that was previously
/// running in single-tenant mode boots up with multi-tenant mode enabled. It stamps every
/// existing row in every tenant-owned table with a default <c>TenantId</c> so the new MT
/// query filter does not silently hide the host's pre-existing data.
/// </para>
/// <para>
/// Hosts that prefer to control the upgrade explicitly should set
/// <see cref="EnableAutomaticUpgrade"/> to <see langword="false"/> and invoke the CLI
/// command <c>dotnet polar-mt upgrade</c> at a controlled maintenance window instead.
/// </para>
/// </remarks>
public sealed class SingleTenantUpgradeOptions
{
    /// <summary>The appsettings section this options class binds to.</summary>
    public const string SectionName = "PolarSharp:MultiTenant:SingleTenantUpgrade";

    /// <summary>
    /// Gets or sets a value indicating whether the upgrade migrator runs automatically on host
    /// startup the first time the host comes up in multi-tenant mode.
    /// </summary>
    /// <value>
    /// Default <see langword="true"/>. Set to <see langword="false"/> when the host prefers to
    /// invoke the upgrade explicitly via the CLI (<c>dotnet polar-mt upgrade</c>) — typically
    /// chosen by operators who want to schedule the backfill during an off-hours maintenance window.
    /// </value>
    public bool EnableAutomaticUpgrade { get; set; } = true;

    /// <summary>
    /// Gets or sets the strategy for picking the tenant that existing single-tenant-mode rows
    /// are assigned to.
    /// </summary>
    /// <value>Default <see cref="DefaultTenantStrategy.LiteralDefault"/>.</value>
    public DefaultTenantStrategy DefaultTenantStrategy { get; set; } = DefaultTenantStrategy.LiteralDefault;

    /// <summary>
    /// Gets or sets the slug (URL-safe identifier) of the auto-created tenant when
    /// <see cref="DefaultTenantStrategy"/> is <see cref="DefaultTenantStrategy.LiteralDefault"/>.
    /// </summary>
    /// <value>
    /// Default <c>"default"</c>. Must match the slug pattern enforced by
    /// <see cref="SingleTenantUpgradeOptionsValidator"/>: lowercase alphanumeric and hyphens,
    /// no leading or trailing hyphens, length 1–64.
    /// </value>
    public string LiteralDefaultTenantSlug { get; set; } = "default";

    /// <summary>
    /// Gets or sets the display name of the auto-created tenant when
    /// <see cref="DefaultTenantStrategy"/> is <see cref="DefaultTenantStrategy.LiteralDefault"/>.
    /// </summary>
    /// <value>Default <c>"Default Tenant"</c>. Used for the tenant's human-readable label only.</value>
    public string LiteralDefaultTenantName { get; set; } = "Default Tenant";

    /// <summary>
    /// Gets or sets a value indicating whether the migrator refuses to start when no
    /// graceful-quiescence marker has been observed from the deploying operator.
    /// </summary>
    /// <value>
    /// Default <see langword="true"/>. Set to <see langword="false"/> only when the operator
    /// genuinely wants a non-graceful upgrade — rare, and only safe on a low-traffic system
    /// where briefly serving partially-stamped reads is acceptable.
    /// </value>
    public bool RequireGracefulQuiescence { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum wall-clock time the migrator may run before aborting.
    /// </summary>
    /// <value>
    /// Default 30 minutes. Hosts with very large single-tenant datasets (millions of rows
    /// across many tables) may need to increase this; the migrator does bulk updates in
    /// provider-native batches but still has to walk every table.
    /// </value>
    public TimeSpan MaxRunDuration { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Strategy for picking the default tenant during a single-tenant -> multi-tenant upgrade.
/// </summary>
/// <remarks>
/// The choice is locked in at upgrade time: subsequent re-runs read the strategy from the
/// completion marker and refuse to switch strategies mid-flight.
/// </remarks>
public enum DefaultTenantStrategy
{
    /// <summary>
    /// Auto-create a tenant with the slug specified by
    /// <see cref="SingleTenantUpgradeOptions.LiteralDefaultTenantSlug"/> (default
    /// <c>"default"</c>). The simplest option — appropriate for solo-operator hosts that
    /// have always been single-tenant and now want a single named tenant to anchor the data.
    /// </summary>
    LiteralDefault = 0,

    /// <summary>
    /// Use the first registered user's owning organization as the default tenant. Requires
    /// the host to have <c>PolarSharp.MultiTenant.Identity</c> registered so the migrator
    /// can resolve the user-to-organization mapping.
    /// </summary>
    /// <remarks>
    /// Not implemented in Stage A — Stage A throws <see cref="NotSupportedException"/> when
    /// this strategy is selected. Available once the Identity package's MT integration ships.
    /// </remarks>
    FirstUserOrganization = 1,

    /// <summary>
    /// The host supplies the default tenant via an <see cref="IDefaultTenantResolver"/>
    /// implementation registered in DI. The most flexible option — appropriate for hosts
    /// that need to look up the tenant from an external source (e.g., a master SaaS registry).
    /// </summary>
    HostSupplied = 2,
}
