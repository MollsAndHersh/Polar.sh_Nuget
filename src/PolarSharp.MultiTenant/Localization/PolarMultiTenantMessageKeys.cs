namespace PolarSharp.MultiTenant.Localization;

/// <summary>
/// Defines all localization key constants used by PolarSharp.MultiTenant.
/// Every key must be present in every supported <c>.resx</c> file.
/// </summary>
internal static class PolarMultiTenantMessageKeys
{
    /// <summary>No tenant has been resolved for the current request.</summary>
    public const string Tenant_NotResolved = nameof(Tenant_NotResolved);

    /// <summary>No PolarSharp configuration was found for the resolved tenant ID.</summary>
    public const string Tenant_NotConfigured = nameof(Tenant_NotConfigured);

    /// <summary>No active multi-tenant context is available in the current DI scope.</summary>
    public const string Tenant_NoActiveContext = nameof(Tenant_NoActiveContext);
}
