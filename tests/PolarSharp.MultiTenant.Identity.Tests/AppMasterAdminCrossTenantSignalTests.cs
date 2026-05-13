using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.Tests;

/// <summary>
/// Verifies the cross-tenant signal flag is initially off, can be granted exactly once, and
/// reads correctly through both the read interface (consumed by the EF Core base) and the
/// write interface (set by the endpoint filter).
/// </summary>
public sealed class AppMasterAdminCrossTenantSignalTests
{
    [Fact]
    public void New_signal_starts_in_locked_down_state()
    {
        var signal = new AppMasterAdminCrossTenantSignal();
        Assert.False(signal.IsAllowedCrossTenantAccess);
    }

    [Fact]
    public void Granting_access_flips_the_flag_to_true()
    {
        var signal = new AppMasterAdminCrossTenantSignal();
        signal.GrantCrossTenantAccess();
        Assert.True(signal.IsAllowedCrossTenantAccess);
    }

    [Fact]
    public void Granting_access_repeatedly_is_idempotent_and_safe()
    {
        var signal = new AppMasterAdminCrossTenantSignal();
        signal.GrantCrossTenantAccess();
        signal.GrantCrossTenantAccess();
        signal.GrantCrossTenantAccess();
        Assert.True(signal.IsAllowedCrossTenantAccess);
    }

    [Fact]
    public void Signal_read_via_Phase_2_interface_returns_same_value()
    {
        var signal = new AppMasterAdminCrossTenantSignal();
        IAppMasterAdminCrossTenantContext readView = signal;
        Assert.False(readView.IsAllowedCrossTenantAccess);

        signal.GrantCrossTenantAccess();
        Assert.True(readView.IsAllowedCrossTenantAccess);
    }
}
