using System.Data.Common;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PolarSharp.MultiTenant;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL;

/// <summary>
/// V20-008: sets PostgreSQL session variables (<c>app.current_tenant_id</c>,
/// <c>app.is_app_master_admin</c>) on every connection open so the row-level-security
/// <c>tenant_isolation</c> POLICY can filter rows by the current tenant. Pairs with the
/// <c>EnableRowLevelSecurity</c> migration that issues <c>ALTER TABLE … ENABLE ROW LEVEL
/// SECURITY</c> and <c>CREATE POLICY</c> on every <c>ITenantOwned</c> table.
/// </summary>
/// <remarks>
/// EF Core fires <see cref="ConnectionOpenedAsync"/> on every check-out from the connection
/// pool, so the session vars get re-set per request even when the underlying TCP connection
/// is reused. <c>set_config(..., false)</c> sets a session-level (not transaction-local)
/// value so it persists for subsequent statements within the connection.
/// </remarks>
public sealed class PostgreSqlTenantSessionInterceptor(
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
        var crossTenant = crossTenantContext.IsAllowedCrossTenantAccess ? "true" : "false";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT set_config('app.current_tenant_id', @TenantId, false), " +
            "set_config('app.is_app_master_admin', @IsAppMasterAdmin, false);";

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
