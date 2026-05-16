using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.MultiTenant.Identity.CosmosDb;

/// <summary>Design-time factory for EF Core tooling against the Identity Cosmos DB DbContext.</summary>
public sealed class PolarUserDbContextCosmosDbDesignTimeFactory : IDesignTimeDbContextFactory<PolarUserDbContext>
{
    /// <inheritdoc/>
    public PolarUserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarUserDbContext>()
            .UseCosmos(
                accountEndpoint: "https://localhost:8081",
                accountKey: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
                databaseName: "polar_identity_design")
            .Options;
        return new PolarUserDbContext(options);
    }
}
