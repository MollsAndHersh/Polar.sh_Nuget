using System.Data.Common;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer;

/// <summary>
/// V20-008: sets SQL Server <c>SESSION_CONTEXT</c> on every connection open so the
/// row-level-security <c>tenant_security_policy</c> can filter rows by the current
/// tenant. Pairs with the <c>EnableRowLevelSecurity</c> migration that creates the
/// <c>tenant_filter</c> table-valued function + <c>SECURITY POLICY</c>.
/// </summary>
/// <remarks>
/// <para>
/// Sets two session keys per connection:
/// <list type="bullet">
///   <item><c>tenant_id</c> — the current tenant's GUID (string), or empty when no tenant is resolved.</item>
///   <item><c>is_app_master_admin</c> — bit, true when an AppMasterAdmin has opted into a cross-tenant route via <c>[AllowCrossTenant]</c>.</item>
/// </list>
/// </para>
/// <para>
/// EF Core fires <see cref="ConnectionOpenedAsync"/> on every connection check-out from
/// the pool, so SESSION_CONTEXT gets re-set per-request even when the underlying TCP
/// connection is reused. The cost is a single round-trip with parameterised values.
/// </para>
/// </remarks>
internal sealed class SqlServerTenantSessionInterceptor(
    IMultiTenantContextAccessor tenantAccessor,
    IAppMasterAdminCrossTenantContext crossTenantContext)
    : DbConnectionInterceptor
{
    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var tenantId = (tenantAccessor.MultiTenantContext?.TenantInfo as PolarTenantInfo)?.Id ?? string.Empty;
        var crossTenant = crossTenantContext.IsAllowedCrossTenantAccess ? 1 : 0;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "EXEC sys.sp_set_session_context @key=N'tenant_id', @value=@TenantId; " +
            "EXEC sys.sp_set_session_context @key=N'is_app_master_admin', @value=@IsAppMasterAdmin;";

        var tidParam = cmd.CreateParameter();
        tidParam.ParameterName = "@TenantId";
        tidParam.Value = tenantId;
        cmd.Parameters.Add(tidParam);

        var flagParam = cmd.CreateParameter();
        flagParam.ParameterName = "@IsAppMasterAdmin";
        flagParam.Value = crossTenant;
        cmd.Parameters.Add(flagParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
