using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PolarSharp.MultiTenant.Identity;

/// <summary>
/// Default <see cref="IAppMasterAdminProvisioning"/> implementation backed by
/// <see cref="UserManager{TUser}"/> and the <see cref="PolarUserDbContext"/>.
/// </summary>
internal sealed class AppMasterAdminProvisioning : IAppMasterAdminProvisioning
{
    private readonly UserManager<PolarApplicationUser> _userManager;
    private readonly ILogger<AppMasterAdminProvisioning> _logger;

    public AppMasterAdminProvisioning(
        UserManager<PolarApplicationUser> userManager,
        ILogger<AppMasterAdminProvisioning> logger)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(logger);
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IdentityResult> GrantAppMasterAdminAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = $"No user with id {userId}." });

        if (user.IsAppMasterAdmin && await _userManager.IsInRoleAsync(user, PolarRoles.AppMasterAdmin).ConfigureAwait(false))
            return IdentityResult.Success;

        user.IsAppMasterAdmin = true;
        var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded) return updateResult;

        var roleResult = await _userManager.AddToRoleAsync(user, PolarRoles.AppMasterAdmin).ConfigureAwait(false);
        if (!roleResult.Succeeded)
        {
            // Roll back the flag so the user doesn't end up half-elevated.
            user.IsAppMasterAdmin = false;
            await _userManager.UpdateAsync(user).ConfigureAwait(false);
            return roleResult;
        }

        _logger.LogWarning("AppMasterAdmin granted: user {UserId} ({Email})", user.Id, user.Email);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> RevokeAppMasterAdminAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = $"No user with id {userId}." });

        // Last-admin guard — never demote the only remaining AppMasterAdmin.
        var activeAdminCount = await _userManager.Users
            .CountAsync(u => u.IsAppMasterAdmin && u.Id != userId, ct).ConfigureAwait(false);
        if (activeAdminCount == 0)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "LastAppMasterAdmin",
                Description = "Cannot revoke the only remaining AppMasterAdmin — there must always be at least one active platform admin.",
            });

        user.IsAppMasterAdmin = false;
        var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded) return updateResult;

        if (await _userManager.IsInRoleAsync(user, PolarRoles.AppMasterAdmin).ConfigureAwait(false))
        {
            var roleResult = await _userManager.RemoveFromRoleAsync(user, PolarRoles.AppMasterAdmin).ConfigureAwait(false);
            if (!roleResult.Succeeded) return roleResult;
        }

        _logger.LogWarning("AppMasterAdmin revoked: user {UserId} ({Email})", user.Id, user.Email);
        return IdentityResult.Success;
    }

    public async Task<IReadOnlyList<PolarApplicationUser>> ListAppMasterAdminsAsync(CancellationToken ct = default)
    {
        var admins = await _userManager.Users
            .Where(u => u.IsAppMasterAdmin)
            .OrderBy(u => u.Email)
            .ToListAsync(ct).ConfigureAwait(false);
        return admins;
    }
}
