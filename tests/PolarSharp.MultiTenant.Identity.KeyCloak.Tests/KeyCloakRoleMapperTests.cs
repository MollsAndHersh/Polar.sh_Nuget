using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.KeyCloak;

namespace PolarSharp.MultiTenant.Identity.KeyCloak.Tests;

public sealed class KeyCloakRoleMapperTests
{
    [Fact]
    public void Mapped_role_is_returned()
    {
        var mapper = NewMapper(("realm-admin", PolarRoles.TenantAdmin));
        Assert.Equal(PolarRoles.TenantAdmin, mapper.Map("realm-admin"));
    }

    [Fact]
    public void Unmapped_role_returns_null()
    {
        var mapper = NewMapper(("realm-admin", PolarRoles.TenantAdmin));
        Assert.Null(mapper.Map("unknown-role"));
    }

    [Fact]
    public void Empty_mapper_returns_null_for_every_input()
    {
        var mapper = NewMapper();
        Assert.Null(mapper.Map("any-role"));
    }

    [Fact]
    public void Blank_role_name_throws()
    {
        var mapper = NewMapper();
        Assert.Throws<ArgumentException>(() => mapper.Map(""));
    }

    [Fact]
    public void MapMany_filters_unmapped_roles_silently()
    {
        var mapper = NewMapper(
            ("realm-admin", PolarRoles.TenantAdmin),
            ("realm-user", PolarRoles.TenantUser));

        var result = mapper.MapMany(["realm-admin", "irrelevant-role", "realm-user", ""]).ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains(PolarRoles.TenantAdmin, result);
        Assert.Contains(PolarRoles.TenantUser, result);
    }

    [Fact]
    public void Mapping_is_case_sensitive_to_match_KeyCloak_role_naming_convention()
    {
        // KeyCloak role names are case-sensitive — the mapper must match exactly.
        var mapper = NewMapper(("realm-admin", PolarRoles.TenantAdmin));
        Assert.Null(mapper.Map("Realm-Admin"));   // different case = different role
    }

    private static KeyCloakRoleMapper NewMapper(params (string kc, string polar)[] mappings)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (kc, polar) in mappings) dict[kc] = polar;
        return new KeyCloakRoleMapper(new KeyCloakOptions { RoleMapping = dict });
    }
}
