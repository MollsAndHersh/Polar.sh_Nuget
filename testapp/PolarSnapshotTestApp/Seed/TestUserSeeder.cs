using Microsoft.AspNetCore.Identity;
using PolarSharp.MultiTenant.Identity;

namespace PolarSnapshotTestApp.Seed;

/// <summary>
/// First-run seeder for the test app's default Identity user. Idempotent — does
/// nothing on subsequent starts. Credentials are intentionally weak (test-only):
/// <c>test@example.com</c> / <c>TestPass123</c>.
/// </summary>
internal sealed class TestUserSeeder(IServiceProvider sp, ILogger<TestUserSeeder> logger) : IHostedService
{
    public const string SeedEmail = "test@example.com";
    public const string SeedPassword = "TestPass123";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = sp.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PolarApplicationUser>>();

        var existing = await users.FindByEmailAsync(SeedEmail).ConfigureAwait(false);
        if (existing is not null)
        {
            logger.LogInformation("PolarSnapshotTestApp: seed user {Email} already exists — skipping.", SeedEmail);
            return;
        }

        var user = new PolarApplicationUser
        {
            UserName = SeedEmail,
            Email = SeedEmail,
            EmailConfirmed = true,
            FullName = "Test User",
            OnboardedAt = DateTimeOffset.UtcNow,
        };

        var result = await users.CreateAsync(user, SeedPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "PolarSnapshotTestApp: failed to seed test user — {Errors}",
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
            return;
        }

        logger.LogInformation(
            "PolarSnapshotTestApp: seeded test user {Email} (password '{Password}'). POST /test/identity/login to drive the snapshot orchestrator.",
            SeedEmail, SeedPassword);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
