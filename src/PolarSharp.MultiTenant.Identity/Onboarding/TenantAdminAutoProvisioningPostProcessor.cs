using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.Onboarding;

namespace PolarSharp.MultiTenant.Identity.Onboarding;

/// <summary>
/// Auto-provisioning hook — when a tenant is onboarded successfully, ensures the
/// <c>InitialAdminEmail</c> from the onboarding result has an active
/// <see cref="PolarRoles.TenantAdmin"/> membership in the new tenant.
/// </summary>
/// <remarks>
/// <para>
/// Implements <c>PolarSharp.Onboarding.IOnboardingPostProcessor</c>; that interface lives in
/// the optional Onboarding package, so this type is referenced by name only — registration
/// happens via reflection in the <c>AddTenantAdminAutoProvisioning</c> extension below.
/// </para>
/// <para>
/// <strong>Idempotent:</strong> if the membership already exists, the hook is a no-op. If
/// the user does not yet exist, the hook creates them with a generated random password and
/// emits a <see cref="LogLevel.Warning"/> with a single-use password reset token (the host
/// is expected to email it; PolarSharp does not send mail itself).
/// </para>
/// </remarks>
public sealed class TenantAdminAutoProvisioningPostProcessor : IOnboardingPostProcessor
{
    private readonly UserManager<PolarApplicationUser> _userManager;
    private readonly RoleManager<PolarApplicationRole> _roleManager;
    private readonly PolarUserDbContext _db;
    private readonly ILogger<TenantAdminAutoProvisioningPostProcessor> _logger;

    /// <summary>Initializes the post-processor.</summary>
    public TenantAdminAutoProvisioningPostProcessor(
        UserManager<PolarApplicationUser> userManager,
        RoleManager<PolarApplicationRole> roleManager,
        PolarUserDbContext db,
        ILogger<TenantAdminAutoProvisioningPostProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessAsync(OnboardedTenantResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!Guid.TryParse(result.TenantId, out var tenantGuid))
        {
            _logger.LogWarning("TenantAdmin auto-provisioning skipped: tenant id '{TenantId}' is not a valid Guid", result.TenantId);
            return;
        }
        await ProvisionAsync(tenantGuid, result.InitialAdminEmail, ct).ConfigureAwait(false);
    }

    /// <summary>Auto-provisions the named user as <see cref="PolarRoles.TenantAdmin"/> in the supplied tenant.</summary>
    /// <param name="tenantId">The Guid identifier of the newly-onboarded tenant.</param>
    /// <param name="adminEmail">Email of the user to be made TenantAdmin. <see langword="null"/> = no-op.</param>
    /// <param name="ct">Cancellation.</param>
    public async Task ProvisionAsync(Guid tenantId, string? adminEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            _logger.LogDebug("TenantAdmin auto-provisioning skipped for tenant {TenantId}: no InitialAdminEmail supplied", tenantId);
            return;
        }

        var role = await _roleManager.FindByNameAsync(PolarRoles.TenantAdmin).ConfigureAwait(false);
        if (role is null)
        {
            _logger.LogWarning("TenantAdmin auto-provisioning skipped for tenant {TenantId}: TenantAdmin role not yet seeded (RoleSeeder may not have run)", tenantId);
            return;
        }

        var user = await _userManager.FindByEmailAsync(adminEmail).ConfigureAwait(false);
        if (user is null)
        {
            // Create a placeholder user with a random password; emit reset token so the host can email it.
            user = new PolarApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                OnboardedAt = DateTimeOffset.UtcNow,
            };
            var createResult = await _userManager.CreateAsync(user, $"!Polar!{Guid.NewGuid():N}!").ConfigureAwait(false);
            if (!createResult.Succeeded)
            {
                _logger.LogError(
                    "TenantAdmin auto-provisioning failed to create user {Email} for tenant {TenantId}: {Errors}",
                    adminEmail, tenantId,
                    string.Join("; ", createResult.Errors.Select(e => $"{e.Code}={e.Description}")));
                return;
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
            _logger.LogWarning(
                "TenantAdmin auto-provisioning created user {Email} for tenant {TenantId}. Single-use reset token (24h): {Token}",
                adminEmail, tenantId, token);
        }

        // Idempotent check — skip if a TenantAdmin membership for this user/tenant already exists.
        var existing = await _db.Memberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == user.Id && m.TenantId == tenantId && m.RoleId == role.Id && m.IsActive, ct)
            .ConfigureAwait(false);
        if (existing) return;

        _db.Memberships.Add(new PolarUserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            RoleId = role.Id,
            JoinedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("TenantAdmin auto-provisioned: user {Email} → tenant {TenantId}", adminEmail, tenantId);
    }
}
