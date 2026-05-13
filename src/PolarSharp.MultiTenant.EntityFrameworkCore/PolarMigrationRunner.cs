using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// Applies any pending EF Core migrations for a specific <see cref="DbContext"/> on host
/// startup. One runner instance per DbContext type — register via
/// <see cref="MigrationRunnerExtensions.RunPolarMigrationsAtStartup{TContext}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Idempotent.</strong> EF Core's <c>__EFMigrationsHistory</c> tracks applied
/// migrations, so re-running the host without schema changes is a no-op. Skips entirely
/// when the configured <see cref="PolarMigrationOptions.Enabled"/> is <see langword="false"/>.
/// </para>
/// <para>
/// <strong>Production posture.</strong> In Production hosts should set
/// <see cref="PolarMigrationOptions.RunOnStartup"/> = <see langword="true"/> and use the
/// real <c>Database.MigrateAsync</c> path. In Development with no migrations yet, hosts can
/// fall back to <see cref="PolarMigrationOptions.UseEnsureCreatedInDevelopment"/> — but that
/// flag throws when set in Production to prevent accidentally bringing up an unversioned DB.
/// </para>
/// </remarks>
public sealed class PolarMigrationRunner<TContext> : IHostedService where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PolarMigrationOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<PolarMigrationRunner<TContext>> _logger;

    /// <summary>Initializes a new migration runner for the supplied <typeparamref name="TContext"/>.</summary>
    public PolarMigrationRunner(
        IServiceScopeFactory scopeFactory,
        PolarMigrationOptions options,
        IHostEnvironment env,
        ILogger<PolarMigrationRunner<TContext>> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _options = options;
        _env = env;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.RunOnStartup)
        {
            _logger.LogInformation(
                "PolarMigrationRunner<{Context}>: skipped (Enabled={Enabled}, RunOnStartup={RunOnStartup})",
                typeof(TContext).Name, _options.Enabled, _options.RunOnStartup);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var hasMigrations = db.Database.GetMigrations().Any();
        if (!hasMigrations)
        {
            if (_env.IsProduction())
            {
                throw new InvalidOperationException(
                    $"PolarMigrationRunner<{typeof(TContext).Name}>: no migrations registered for this DbContext, " +
                    "and we are in Production. Generate the initial migration via " +
                    $"`dotnet ef migrations add Initial --context {typeof(TContext).Name}` " +
                    "and re-deploy. Refusing to use EnsureCreatedAsync in Production — that path " +
                    "produces an unversioned schema you can never upgrade safely.");
            }

            if (!_options.UseEnsureCreatedInDevelopment)
            {
                _logger.LogWarning(
                    "PolarMigrationRunner<{Context}>: no migrations registered AND UseEnsureCreatedInDevelopment=false. Skipping schema setup — your host must call EnsureCreatedAsync explicitly.",
                    typeof(TContext).Name);
                return;
            }

            _logger.LogWarning(
                "PolarMigrationRunner<{Context}>: no migrations registered — falling back to EnsureCreatedAsync (Development only). Generate migrations before deploying to Production.",
                typeof(TContext).Name);
            await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        if (pending.Count == 0)
        {
            _logger.LogInformation("PolarMigrationRunner<{Context}>: schema up to date — no pending migrations.", typeof(TContext).Name);
            return;
        }

        _logger.LogInformation(
            "PolarMigrationRunner<{Context}>: applying {Count} pending migration(s): {Migrations}",
            typeof(TContext).Name, pending.Count, string.Join(", ", pending));

        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("PolarMigrationRunner<{Context}>: migrations applied successfully.", typeof(TContext).Name);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Configuration for the migration runner. Bound from
/// <c>PolarSharp:MultiTenant:Migrations</c> by default; provider packages may bind to a
/// nested section per DbContext.
/// </summary>
public sealed class PolarMigrationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PolarSharp:MultiTenant:Migrations";

    /// <summary>Master switch. Default <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When <see langword="true"/>, the runner applies migrations on host startup. Default <see langword="true"/>.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>When <see langword="true"/> AND no migrations are registered AND the environment is NOT Production, the runner falls back to <c>EnsureCreatedAsync</c>. Default <see langword="true"/>.</summary>
    public bool UseEnsureCreatedInDevelopment { get; set; } = true;
}
