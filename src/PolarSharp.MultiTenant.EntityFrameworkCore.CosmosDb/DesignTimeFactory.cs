using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb;

/// <summary>
/// Design-time factory for EF Core tooling against the Cosmos DB tenant DbContext.
/// </summary>
/// <remarks>
/// Cosmos has no schema migration concept (Cosmos documents are schemaless), so this
/// factory exists primarily so EF Core tools (<c>dotnet ef dbcontext info</c>,
/// <c>dotnet ef dbcontext optimize</c>) can introspect the model. Migration commands
/// will report that Cosmos doesn't support migrations.
/// </remarks>
public sealed class PolarTenantDbContextCosmosDbDesignTimeFactory : IDesignTimeDbContextFactory<PolarTenantDbContext>
{
    /// <summary>Builds a DbContext for the EF design-time tools.</summary>
    public PolarTenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarTenantDbContext>()
            .UseCosmos(
                accountEndpoint: "https://localhost:8081",   // Cosmos DB Emulator default
                accountKey: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",  // Emulator's well-known design-time-only key (public; not a real secret)
                databaseName: "polar_tenants_design")
            .Options;
        return new PolarTenantDbContext(options);
    }
}
