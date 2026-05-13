namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Options that control PolarSharp Identity behavior. Bound from
/// <c>PolarSharp:Identity</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class PolarIdentityOptions
{
    /// <summary>The configuration root section name.</summary>
    public const string SectionName = "PolarSharp:Identity";

    /// <summary>When <see langword="true"/>, host startup blocks if any tenant has no active <see cref="PolarRoles.TenantAdmin"/> membership. Default: <see langword="true"/>.</summary>
    public bool RequireTenantAdminInvariant { get; set; } = true;

    /// <summary>SQL connection settings — populated from <c>appsettings.json</c> at <c>PolarSharp:Identity:Sql</c>.</summary>
    public SqlOptions Sql { get; set; } = new();

    /// <summary>SQL connection settings for the Identity database.</summary>
    /// <remarks>
    /// <para>
    /// <strong>Three coexisting deployment shapes</strong>, all driven from configuration:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <strong>Dedicated DB, dedicated <see cref="PolarUserDbContext"/></strong> — set
    ///     <see cref="ConnectionString"/> (or <see cref="ConnectionStringName"/>) and call
    ///     the provider's <c>UseSqlServer()</c> / <c>UseSqlite()</c> / <c>UsePostgreSql()</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <strong>Shared DB, dedicated <see cref="PolarUserDbContext"/></strong> — same as
    ///     #1 but using the host's main connection string. Identity tables coexist with the
    ///     host's tables in one database.
    ///   </description></item>
    ///   <item><description>
    ///     <strong>Shared DB, host's own DbContext</strong> — set <see cref="UseHostDbContext"/>
    ///     to <see langword="true"/> and call the provider's <c>UseHostDbContext&lt;TContext&gt;()</c>
    ///     overload. The host's DbContext derives from <see cref="PolarUserDbContext"/> (or
    ///     applies <c>ModelBuilder.AddPolarIdentitySchema()</c> in its <c>OnModelCreating</c>).
    ///     PolarSharp uses the host's already-registered DbContext directly — single
    ///     <see cref="Microsoft.EntityFrameworkCore.DbContext"/> for the entire app.
    ///   </description></item>
    /// </list>
    /// </remarks>
    public sealed class SqlOptions
    {
        /// <summary>The SQL provider — must be set when not using the host's own DbContext. Values: <c>SqlServer</c>, <c>Sqlite</c>, <c>PostgreSql</c>.</summary>
        public string? Provider { get; set; }

        /// <summary>Direct SQL connection string. Takes precedence over <see cref="ConnectionStringName"/>.</summary>
        public string? ConnectionString { get; set; }

        /// <summary>Name of a connection string in <c>ConnectionStrings:{name}</c>. Used when <see cref="ConnectionString"/> is null. Useful for sharing the host's main connection: set this to e.g. <c>"DefaultConnection"</c>.</summary>
        public string? ConnectionStringName { get; set; }

        /// <summary>When <see langword="true"/>, PolarSharp Identity uses the host's already-registered <see cref="Microsoft.EntityFrameworkCore.DbContext"/> instead of registering its own. Pair with <c>UseHostDbContext&lt;TContext&gt;()</c> at startup. Default: <see langword="false"/>.</summary>
        public bool UseHostDbContext { get; set; }

        /// <summary>When <see langword="true"/>, EF Core migrations are applied automatically on first startup. Default: <see langword="true"/>.</summary>
        public bool EnableMigrationsOnStartup { get; set; } = true;
    }

    /// <summary>Bootstrap configuration for the first AppMasterAdmin.</summary>
    public BootstrapOptions Bootstrap { get; set; } = new();

    /// <summary>Cross-tenant access auditing and rate-limiting.</summary>
    public CrossTenantAccessOptions CrossTenantAccess { get; set; } = new();

    /// <summary>The first AppMasterAdmin bootstrap settings.</summary>
    public sealed class BootstrapOptions
    {
        /// <summary>Email address for the first AppMasterAdmin. Required on first startup.</summary>
        public string? AppMasterAdminEmail { get; set; }

        /// <summary>When <see langword="true"/>, Production startup is blocked until the bootstrap reset password is completed. Default: <see langword="true"/>.</summary>
        public bool BlockProductionStartUntilResetCompleted { get; set; } = true;
    }

    /// <summary>Settings governing AppMasterAdmin cross-tenant operations.</summary>
    public sealed class CrossTenantAccessOptions
    {
        /// <summary>When <see langword="true"/>, every <c>[AllowCrossTenant]</c> operation must include free-form justification text. Default: <see langword="true"/>.</summary>
        public bool RequireJustificationText { get; set; } = true;

        /// <summary>Retention period (days) for entries in the <c>polar_platform_audit_log</c> table. Default: 2555 (~7 years).</summary>
        public int AuditPlatformLogRetentionDays { get; set; } = 2555;

        /// <summary>When <see langword="true"/>, emit a <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/> when an AppMasterAdmin issues more than <see cref="UnusualVolumeThresholdPerHour"/> cross-tenant ops in any rolling hour. Default: <see langword="true"/>.</summary>
        public bool AlertOnUnusualVolume { get; set; } = true;

        /// <summary>The hourly cross-tenant-op threshold above which an alert fires. Default: 50.</summary>
        public int UnusualVolumeThresholdPerHour { get; set; } = 50;
    }
}
