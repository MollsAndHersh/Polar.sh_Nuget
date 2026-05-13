using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// One-time startup task — creates the very first AppMasterAdmin from configuration when no
/// AppMasterAdmin exists in the database.
/// </summary>
/// <remarks>
/// <para>
/// Solves the bootstrap chicken-and-egg problem: <see cref="IAppMasterAdminProvisioning"/>
/// requires an existing AppMasterAdmin to grant the flag, but the very first one has nobody
/// to authorize them. This service runs ONCE at startup, sources the bootstrap email from
/// <c>PolarSharp:Identity:Bootstrap:AppMasterAdminEmail</c>, creates the user, and logs a
/// single-use password reset token at <see cref="LogLevel.Critical"/>.
/// </para>
/// <para>
/// In <see cref="HostingEnvironmentExtensions.IsProduction"/>, when
/// <see cref="PolarIdentityOptions.BootstrapOptions.BlockProductionStartUntilResetCompleted"/>
/// is <see langword="true"/>, the service throws on startup until the reset is completed
/// (detected by the user's <see cref="IdentityUser{TKey}.PasswordHash"/> changing from the
/// initial random placeholder).
/// </para>
/// </remarks>
public sealed class AppMasterAdminBootstrapper : IHostedService
{
    private const string BootstrapPlaceholderPasswordMarker = "POLAR_BOOTSTRAP_PLACEHOLDER";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PolarIdentityOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AppMasterAdminBootstrapper> _logger;

    /// <summary>Initializes a new bootstrapper.</summary>
    public AppMasterAdminBootstrapper(
        IServiceScopeFactory scopeFactory,
        IOptions<PolarIdentityOptions> options,
        IHostEnvironment env,
        ILogger<AppMasterAdminBootstrapper> logger)
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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PolarApplicationUser>>();

        var existingAdminCount = await userManager.Users
            .CountAsync(u => u.IsAppMasterAdmin, cancellationToken).ConfigureAwait(false);

        if (existingAdminCount > 0)
        {
            // An AppMasterAdmin already exists. If we're in Production and the user is still
            // carrying the bootstrap placeholder password, we MUST block startup until the
            // reset is completed.
            if (_options.Value.Bootstrap.BlockProductionStartUntilResetCompleted && _env.IsProduction())
            {
                var unresetAdmin = await userManager.Users
                    .FirstOrDefaultAsync(u => u.IsAppMasterAdmin && u.PasswordHash != null && u.PasswordHash.Contains(BootstrapPlaceholderPasswordMarker), cancellationToken)
                    .ConfigureAwait(false);
                if (unresetAdmin is not null)
                {
                    throw new InvalidOperationException(
                        "Production startup blocked: AppMasterAdmin bootstrap requires the password reset to be completed before serving traffic. " +
                        $"Email: {unresetAdmin.Email}. Check earlier logs for the reset token, complete the reset, then restart.");
                }
            }
            return;
        }

        var email = _options.Value.Bootstrap.AppMasterAdminEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "No AppMasterAdmin exists and PolarSharp:Identity:Bootstrap:AppMasterAdminEmail is not set. " +
                "Configure the bootstrap email and restart.");
        }

        var user = new PolarApplicationUser
        {
            Email = email,
            UserName = email,
            EmailConfirmed = false,
            IsAppMasterAdmin = true,
            OnboardedAt = DateTimeOffset.UtcNow,
            // Marker password embedded in the placeholder so we can detect "still on bootstrap creds":
            PasswordHash = $"!{BootstrapPlaceholderPasswordMarker}!{Guid.NewGuid():N}!",
        };
        var createResult = await userManager.CreateAsync(user).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                "AppMasterAdmin bootstrap failed to create the seed user: " +
                string.Join("; ", createResult.Errors.Select(e => $"{e.Code}={e.Description}")));
        }

        var roleResult = await userManager.AddToRoleAsync(user, PolarRoles.AppMasterAdmin).ConfigureAwait(false);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "AppMasterAdmin bootstrap failed to assign the AppMasterAdmin role: " +
                string.Join("; ", roleResult.Errors.Select(e => $"{e.Code}={e.Description}")));
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);

        _logger.LogCritical(
            """

            ╔════════════════════════════════════════════════════════════════════════╗
            ║ POLARSHARP BOOTSTRAP: AppMasterAdmin '{Email}' CREATED.
            ║ Single-use password-reset token (24h validity):
            ║ {Token}
            ║ Complete the reset before continuing in Production.
            ║ This message will not repeat once an AppMasterAdmin exists with a non-placeholder password.
            ╚════════════════════════════════════════════════════════════════════════╝
            """,
            email, token);

        if (_options.Value.Bootstrap.BlockProductionStartUntilResetCompleted && _env.IsProduction())
        {
            throw new InvalidOperationException(
                "Production startup blocked: complete the AppMasterAdmin password reset, then restart. " +
                "See the Critical log entry above for the reset token.");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
