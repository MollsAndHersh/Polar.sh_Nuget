namespace PolarSharp.EcommerceStoreManagement.Services;

/// <summary>
/// The host implements this to surface "who is performing this mutation" to the audit log
/// interceptor. When <c>PolarSharp.MultiTenant.Identity</c> is installed, the default
/// implementation resolves the actor from <c>ICurrentUser</c>; hosts using different auth
/// stacks register their own implementation.
/// </summary>
public interface IAuditLogActorProvider
{
    /// <summary>Returns the actor performing the current operation, or a synthetic <c>"system"</c> actor when none is resolvable.</summary>
    AuditActor GetCurrentActor();
}

/// <summary>Snapshot of the user performing an audit-logged mutation.</summary>
/// <param name="UserId">Stable identifier of the user.</param>
/// <param name="Email">Email at the time of the operation (snapshotted).</param>
/// <param name="IsAppMasterAdmin">True when the user is a SaaS-provider AppMasterAdmin.</param>
/// <param name="CurrentTenantId">The tenant the user is acting within. <see langword="null"/> for AppMasterAdmin operating site-globally.</param>
public sealed record AuditActor(Guid UserId, string Email, bool IsAppMasterAdmin, Guid? CurrentTenantId)
{
    /// <summary>The fallback actor used when no real user is resolvable (e.g. background jobs).</summary>
    public static AuditActor System { get; } = new(Guid.Empty, "system@polarsharp.local", IsAppMasterAdmin: false, CurrentTenantId: null);
}

/// <summary>Default <see cref="IAuditLogActorProvider"/> returning the <see cref="AuditActor.System"/> actor — used when no host-supplied implementation is registered.</summary>
internal sealed class SystemAuditLogActorProvider : IAuditLogActorProvider
{
    public AuditActor GetCurrentActor() => AuditActor.System;
}
