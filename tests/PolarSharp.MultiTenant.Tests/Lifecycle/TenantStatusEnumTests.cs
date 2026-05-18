using System.Text.Json.Serialization;
using PolarSharp.MultiTenant.Lifecycle;

namespace PolarSharp.MultiTenant.Tests.Lifecycle;

/// <summary>
/// Smoke tests for <see cref="TenantStatus"/>. These guard the enum's wire-format-equivalent
/// integer values (so persisted rows don't shift meaning across versions) and the documented
/// default value used by <c>PolarTenantInfo.Status</c>.
/// </summary>
public sealed class TenantStatusEnumTests
{
    [Fact]
    public void Enum_has_expected_int_values()
    {
        // Persisted as the int value in the tenant store — these MUST stay stable across
        // versions or stored rows will silently shift meaning.
        Assert.Equal(0, (int)TenantStatus.Active);
        Assert.Equal(1, (int)TenantStatus.Suspended);
        Assert.Equal(2, (int)TenantStatus.Inactive);
        Assert.Equal(3, (int)TenantStatus.Deleted);
    }

    [Fact]
    public void Enum_has_expected_named_members()
    {
        var names = Enum.GetNames<TenantStatus>();
        Assert.Equal(4, names.Length);
        Assert.Contains("Active", names);
        Assert.Contains("Suspended", names);
        Assert.Contains("Inactive", names);
        Assert.Contains("Deleted", names);
    }

    [Fact]
    public void Enum_default_value_is_Active()
    {
        // default(TenantStatus) MUST be Active so a freshly-constructed PolarTenantInfo
        // (which initialises Status with the enum default) is treated as active without
        // ceremony.
        Assert.Equal(TenantStatus.Active, default(TenantStatus));
    }

    [Fact]
    public void Enum_does_not_carry_JsonStringEnumConverter_attribute()
    {
        // TenantStatus is a PolarSharp-internal lifecycle marker; it is NOT serialised over
        // the wire to Polar.sh's API and therefore deliberately does not carry the
        // [JsonConverter(typeof(JsonStringEnumConverter))] attribute that the Polar-API
        // enums in PolarSharp.BaseEntities/Enums all carry. This test guards the contrast
        // so a future refactor doesn't accidentally make the on-disk integer values change
        // to strings (which would break backwards-compatible loading of existing tenant rows).
        var attr = typeof(TenantStatus).GetCustomAttributes(typeof(JsonConverterAttribute), inherit: false);
        Assert.Empty(attr);
    }
}
