using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant.EntityFrameworkCore;

namespace PolarSharp.Onboarding.Wizard;

/// <summary>
/// Background service that prunes expired or finalized
/// <see cref="OnboardingSessionEntity"/> rows. Runs on a configurable cadence
/// (default 24h).
/// </summary>
public sealed class OnboardingSessionExpirationCleaner : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PolarOnboardingOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<OnboardingSessionExpirationCleaner> _logger;

    /// <summary>Initializes the cleaner.</summary>
    public OnboardingSessionExpirationCleaner(
        IServiceScopeFactory scopeFactory,
        IOptions<PolarOnboardingOptions> options,
        TimeProvider time,
        ILogger<OnboardingSessionExpirationCleaner> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _options = options;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, _options.Value.Wizard.ExpirationCleanerIntervalHours));
        using var timer = new PeriodicTimer(interval, _time);
        do
        {
            await PruneAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
            var now = _time.GetUtcNow();

            var expired = await db.Set<OnboardingSessionEntity>()
                .Where(s => s.IsCanceled || s.IsFinished || s.ExpiresAt <= now)
                .ToListAsync(ct).ConfigureAwait(false);

            if (expired.Count == 0) return;

            db.Set<OnboardingSessionEntity>().RemoveRange(expired);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("OnboardingSessionExpirationCleaner pruned {Count} expired/finalized wizard sessions", expired.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnboardingSessionExpirationCleaner failed to prune; will retry on next tick");
        }
    }
}
