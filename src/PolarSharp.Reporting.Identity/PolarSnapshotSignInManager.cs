using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.Reporting.Snapshot;
using System.Security.Claims;

namespace PolarSharp.Reporting.Identity;

/// <summary>
/// Decorator over <see cref="SignInManager{TUser}"/> that drives the per-tenant snapshot
/// orchestrator on user sign-in / sign-out. V20-005 Phase 3: when a user signs in to a
/// tenant, fires <see cref="IReportSnapshotTrigger.TriggerImmediateAsync"/> + starts
/// periodic polling at the configured cadence; when the user signs out, stops polling.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why a SignInManager subclass:</strong> ASP.NET Core Identity's <c>SignInAsync</c>
/// and <c>SignOutAsync</c> are the canonical authentication boundaries — they fire AFTER
/// credential validation (no spurious snapshot triggers on a failed login) and BEFORE the
/// user's first authenticated request (the snapshot starts ingesting before they hit a
/// reporting page). Subclassing avoids forcing host code to remember to call the trigger
/// from every login pathway.
/// </para>
/// <para>
/// <strong>Tenant resolution:</strong> the orchestrator is per-tenant, so the bridge needs
/// the current tenant id at sign-in time. We pull from <c>IMultiTenantContextAccessor&lt;PolarTenantInfo&gt;</c>
/// (Finbuckle's per-request context) — assumed populated by the same middleware that
/// resolved the tenant for the rest of the auth pipeline. If no tenant is resolved at
/// sign-in time (unusual), the bridge logs a warning and skips the trigger; the user can
/// still log in successfully.
/// </para>
/// <para>
/// <strong>Failure isolation:</strong> a failure inside <see cref="IReportSnapshotTrigger"/>
/// must NEVER block the user's login or logout. Every orchestrator call is wrapped in
/// try/catch with the exception logged at <c>Warning</c>; sign-in/sign-out always returns
/// the underlying base result.
/// </para>
/// </remarks>
public sealed class PolarSnapshotSignInManager : SignInManager<PolarApplicationUser>
{
    private readonly IReportSnapshotTrigger _trigger;
    private readonly IMultiTenantContextAccessor<PolarTenantInfo> _tenantAccessor;
    private readonly SnapshotTriggerOptions _options;
    private readonly ILogger<PolarSnapshotSignInManager> _bridgeLogger;

    /// <summary>Creates the decorator. Constructor mirrors the base <see cref="SignInManager{TUser}"/> shape.</summary>
    public PolarSnapshotSignInManager(
        UserManager<PolarApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<PolarApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<PolarApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<PolarApplicationUser> confirmation,
        IReportSnapshotTrigger trigger,
        IMultiTenantContextAccessor<PolarTenantInfo> tenantAccessor,
        IOptions<SnapshotTriggerOptions> snapshotOptions,
        ILogger<PolarSnapshotSignInManager> bridgeLogger)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
        _options = snapshotOptions?.Value ?? throw new ArgumentNullException(nameof(snapshotOptions));
        _bridgeLogger = bridgeLogger ?? throw new ArgumentNullException(nameof(bridgeLogger));
    }

    /// <inheritdoc/>
    public override async Task SignInAsync(PolarApplicationUser user, AuthenticationProperties authenticationProperties, string? authenticationMethod = null)
    {
        await base.SignInAsync(user, authenticationProperties, authenticationMethod).ConfigureAwait(false);
        await FireSnapshotOnSignInAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task SignInWithClaimsAsync(PolarApplicationUser user, AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
    {
        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims).ConfigureAwait(false);
        await FireSnapshotOnSignInAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task SignOutAsync()
    {
        var tenantId = ResolveTenantId();
        await base.SignOutAsync().ConfigureAwait(false);
        if (tenantId is null) return;

        try
        {
            await _trigger.StopPeriodicAsync(tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _bridgeLogger.LogWarning(ex,
                "PolarSharp.Reporting.Identity: failed to stop periodic snapshot for tenant {TenantId} on sign-out — sign-out itself succeeded.",
                tenantId);
        }
    }

    private async Task FireSnapshotOnSignInAsync()
    {
        var tenantId = ResolveTenantId();
        if (tenantId is null)
        {
            _bridgeLogger.LogWarning(
                "PolarSharp.Reporting.Identity: user signed in but no tenant context resolved — snapshot trigger skipped.");
            return;
        }

        try
        {
            await _trigger.TriggerImmediateAsync(tenantId, "Login", CancellationToken.None).ConfigureAwait(false);
            await _trigger.StartPeriodicAsync(tenantId, _options.DefaultInterval).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _bridgeLogger.LogWarning(ex,
                "PolarSharp.Reporting.Identity: snapshot trigger failed on sign-in for tenant {TenantId} — sign-in itself succeeded.",
                tenantId);
        }
    }

    private string? ResolveTenantId()
    {
        var tenantInfo = _tenantAccessor.MultiTenantContext?.TenantInfo as PolarTenantInfo;
        return string.IsNullOrEmpty(tenantInfo?.Id) ? null : tenantInfo.Id;
    }
}
