using Microsoft.Extensions.Configuration;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity.Tests;

/// <summary>
/// Verifies the connection-string resolution priority: explicit argument wins, then bound
/// options' direct value, then bound options' connection-string-name pointer.
/// </summary>
public sealed class IdentityConnectionResolverTests
{
    [Fact]
    public void Explicit_connection_string_takes_top_priority()
    {
        var config = new ConfigurationBuilder().Build();
        var options = new PolarIdentityOptions.SqlOptions
        {
            ConnectionString = "from-options",
            ConnectionStringName = "ignored",
        };

        var resolved = IdentityConnectionResolver.Resolve("explicit-arg", config, options);
        Assert.Equal("explicit-arg", resolved);
    }

    [Fact]
    public void Falls_back_to_bound_options_ConnectionString_when_no_explicit_argument()
    {
        var config = new ConfigurationBuilder().Build();
        var options = new PolarIdentityOptions.SqlOptions
        {
            ConnectionString = "from-options",
        };

        var resolved = IdentityConnectionResolver.Resolve(null, config, options);
        Assert.Equal("from-options", resolved);
    }

    [Fact]
    public void Resolves_named_connection_string_from_IConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=shared-host-db;",
            })
            .Build();

        var options = new PolarIdentityOptions.SqlOptions
        {
            ConnectionStringName = "DefaultConnection",
        };

        var resolved = IdentityConnectionResolver.Resolve(null, config, options);
        Assert.Equal("Server=shared-host-db;", resolved);
    }

    [Fact]
    public void Throws_when_named_connection_string_is_missing()
    {
        var config = new ConfigurationBuilder().Build();
        var options = new PolarIdentityOptions.SqlOptions
        {
            ConnectionStringName = "NotPresent",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => IdentityConnectionResolver.Resolve(null, config, options));
        Assert.Contains("NotPresent", ex.Message);
    }

    [Fact]
    public void Throws_when_no_connection_string_is_supplied_anywhere()
    {
        var config = new ConfigurationBuilder().Build();
        var options = new PolarIdentityOptions.SqlOptions();

        var ex = Assert.Throws<InvalidOperationException>(() => IdentityConnectionResolver.Resolve(null, config, options));
        Assert.Contains("no connection string", ex.Message);
        Assert.Contains("UseHostDbContext", ex.Message);
    }
}
