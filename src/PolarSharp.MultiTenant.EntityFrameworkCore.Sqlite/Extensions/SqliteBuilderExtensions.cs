using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore.Extensions;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Upgrade;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite;

/// <summary>SQLite-specific registration extensions for the PolarSharp tenant store.</summary>
/// <remarks>
/// <para>
/// <strong>The <c>master_SaaS.db</c> convention.</strong> Platform-level data — the tenant
/// registry, the one-time upgrade history, and any future cross-tenant platform tables —
/// live together in a single <c>master_SaaS.db</c> file at the configured database
/// directory. Each tenant (including the single tenant in single-tenant deployments) gets
/// its own <c>{tenantId}.db</c> file for tenant-scoped data; nothing tenant-owned ever
/// touches <c>master_SaaS.db</c>. The naming makes the platform / tenant boundary obvious
/// to operators inspecting the filesystem and aligns with how Litestream replication is
/// configured per-file in Stage C.
/// </para>
/// <para>
/// <strong>Backward compatibility with <c>__tenants.db</c>.</strong> The pre-v1.2 SQLite
/// provider used <c>__tenants.db</c> as the registry filename. When that file exists at the
/// target directory and <c>master_SaaS.db</c> does not, the registration falls back to the
/// legacy filename for this run and logs a warning recommending the operator run the
/// single-tenant upgrade migrator (which renames the file as part of its work) or rename
/// the file manually during a maintenance window. The fallback path never deletes data.
/// </para>
/// <para>
/// <strong>WAL is the default journal mode.</strong> The SQLite connection string sets
/// <c>Journal Mode=Wal</c> so the registry supports concurrent reads with a writer in
/// flight — essential for both Litestream replication (Stage C) and any non-trivial
/// production workload. Hosts that need to override (e.g., for a memory-only test fixture)
/// can build their own connection string and replace the DbContext registration.
/// </para>
/// </remarks>
public static class SqliteBuilderExtensions
{
    /// <summary>The current platform-data filename used by the SQLite tenant store.</summary>
    /// <remarks>
    /// Holds the tenant registry, the upgrade-history table, and future platform-scoped
    /// tables. Replaces the legacy <see cref="LegacyTenantsFileName"/>.
    /// </remarks>
    public const string MasterSaasFileName = "master_SaaS.db";

    /// <summary>The pre-v1.2 platform-data filename, retained for backward compatibility.</summary>
    /// <remarks>
    /// When this file is present in the database directory and <see cref="MasterSaasFileName"/>
    /// is not, the provider falls back to it for the current run and logs a warning. The
    /// single-tenant upgrade migrator renames it to the new name as part of its work.
    /// </remarks>
    public const string LegacyTenantsFileName = "__tenants.db";

    /// <summary>
    /// Registers a SQLite-backed EF Core tenant store. Platform-level data (the tenant
    /// registry, the upgrade-history table) lives in a shared <c>master_SaaS.db</c> file at
    /// <paramref name="databaseDirectory"/>; per-tenant catalog / identity / reporting DBs
    /// (when those packages are also installed) live in <c>{tenantId}.db</c> files in the
    /// same directory. WAL journal mode is enabled by default on the master file.
    /// </summary>
    /// <param name="builder">The PolarSharp infrastructure builder returned by <c>AddPolarMultiTenant()</c>.</param>
    /// <param name="databaseDirectory">Filesystem path where the SQLite database files live. Created if absent.</param>
    /// <param name="seedFromAppSettings">
    /// When <see langword="true"/> and the tenants table is empty on first startup, copies
    /// every <c>PolarSharp:MultiTenant:Tenants[*]</c> entry from <c>appsettings.json</c>.
    /// </param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Always registers <see cref="SqliteSingleTenantUpgradeMigrator"/> as the
    /// <see cref="ISingleTenantUpgradeMigrator"/> for the SQLite provider. The migrator only
    /// runs when the host has also called
    /// <c>services.AddPolarSingleTenantUpgrade(builder.Configuration)</c> — that registration
    /// is the gate, not this one. Registering the migrator unconditionally keeps the
    /// extension method side-effect-free with respect to whether the host has opted in.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant()
    ///     .UseSqlite("/var/lib/polarsharp/tenants/");
    /// </code>
    /// </example>
    public static PolarInfrastructureBuilder UseSqlite(
        this PolarInfrastructureBuilder builder,
        string databaseDirectory,
        bool seedFromAppSettings = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(databaseDirectory);

        Directory.CreateDirectory(databaseDirectory);

        var masterPath = Path.Combine(databaseDirectory, MasterSaasFileName);
        var legacyPath = Path.Combine(databaseDirectory, LegacyTenantsFileName);

        var (resolvedPath, usingLegacy) = ResolveMasterPath(masterPath, legacyPath);
        var connectionString = BuildMasterConnectionString(resolvedPath);

        EfTenantStoreBuilderExtensions.AddCoreServices(builder.Services, builder.Configuration);

        builder.Services.AddDbContext<PolarTenantDbContext>(opts =>
            opts.UseSqlite(connectionString, sql =>
                sql.MigrationsAssembly(typeof(SqliteBuilderExtensions).Assembly.GetName().Name)));
        builder.Services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();

        // SQLite-specific single-tenant -> MT upgrade migrator. Always registered; only
        // executed when the host has also called AddPolarSingleTenantUpgrade(...).
        builder.Services.TryAddScoped<ISingleTenantUpgradeMigrator, SqliteSingleTenantUpgradeMigrator>();

        // Expose the resolved master-file path + directory to the migrator via DI so it does
        // not have to recompute the path from configuration.
        builder.Services.AddSingleton(new SqliteMasterDatabaseLocator(
            DatabaseDirectory: databaseDirectory,
            MasterDatabasePath: resolvedPath,
            UsingLegacyFileName: usingLegacy));

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarTenantDbContext>(
                name: "polar-tenant-sql",
                tags: ["polar-sql", "polar-tenant"]);

        if (seedFromAppSettings)
        {
            builder.Services.AddHostedService<AppSettingsSeeder>();
        }

        if (usingLegacy)
        {
            builder.Services.AddHostedService<LegacyTenantsFileWarningHostedService>();
        }

        return builder;
    }

    /// <summary>
    /// Decides which file the SQLite connection points at: prefers <c>master_SaaS.db</c>;
    /// falls back to a pre-existing <c>__tenants.db</c> when the new file is absent.
    /// </summary>
    /// <param name="masterPath">Absolute path to <c>master_SaaS.db</c>.</param>
    /// <param name="legacyPath">Absolute path to the legacy <c>__tenants.db</c>.</param>
    /// <returns>The chosen path and a flag indicating whether the legacy name was selected.</returns>
    private static (string Path, bool UsingLegacy) ResolveMasterPath(string masterPath, string legacyPath)
    {
        if (File.Exists(masterPath))
        {
            return (masterPath, false);
        }
        if (File.Exists(legacyPath))
        {
            return (legacyPath, true);
        }
        return (masterPath, false);
    }

    /// <summary>
    /// Builds the connection string for the master platform-data file with WAL journaling
    /// enabled. Cache is shared so multiple connections within the same process see writes
    /// without round-tripping through the disk page cache.
    /// </summary>
    /// <param name="path">Absolute filesystem path to the master SQLite database file.</param>
    /// <returns>A Microsoft.Data.Sqlite-compatible connection string.</returns>
    private static string BuildMasterConnectionString(string path)
        => $"Data Source={path};Cache=Shared;Mode=ReadWriteCreate;Journal Mode=Wal";
}

/// <summary>
/// Captures the resolved location of the SQLite master platform-data file so downstream
/// services (the upgrade migrator, future Litestream wiring) can find it without
/// recomputing the path.
/// </summary>
/// <param name="DatabaseDirectory">The directory containing the master file and per-tenant <c>{tenantId}.db</c> files.</param>
/// <param name="MasterDatabasePath">Absolute path to the file the SQLite connection actually opens.</param>
/// <param name="UsingLegacyFileName">
/// <see langword="true"/> when the registration fell back to <c>__tenants.db</c> because
/// <c>master_SaaS.db</c> was absent and the legacy file was present.
/// </param>
public sealed record SqliteMasterDatabaseLocator(
    string DatabaseDirectory,
    string MasterDatabasePath,
    bool UsingLegacyFileName);

/// <summary>
/// Logs a warning at host startup when the SQLite provider fell back to the legacy
/// <c>__tenants.db</c> filename. Operates once and exits.
/// </summary>
/// <remarks>
/// The warning is intentionally logged from a hosted service rather than during DI
/// configuration so it surfaces in the structured-log pipeline with the rest of the
/// startup events. The hosted service is only registered when the fallback was actually
/// taken — there is no warning when both files are absent or when the new file is present.
/// </remarks>
internal sealed class LegacyTenantsFileWarningHostedService : Microsoft.Extensions.Hosting.IHostedService
{
    private readonly SqliteMasterDatabaseLocator _locator;
    private readonly ILogger<LegacyTenantsFileWarningHostedService> _logger;

    /// <summary>Initializes a new <see cref="LegacyTenantsFileWarningHostedService"/>.</summary>
    /// <param name="locator">The resolved master-file location.</param>
    /// <param name="logger">Logger.</param>
    public LegacyTenantsFileWarningHostedService(
        SqliteMasterDatabaseLocator locator,
        ILogger<LegacyTenantsFileWarningHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(logger);
        _locator = locator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "PolarSharp SQLite tenant store: opened legacy file '{LegacyPath}' because the " +
            "v1.2 master file '{MasterFileName}' is absent. The file should be renamed " +
            "during a maintenance window — either manually (shut down the host, rename, " +
            "restart) or via the single-tenant upgrade migrator (which renames as part of " +
            "its work).",
            _locator.MasterDatabasePath,
            SqliteBuilderExtensions.MasterSaasFileName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
