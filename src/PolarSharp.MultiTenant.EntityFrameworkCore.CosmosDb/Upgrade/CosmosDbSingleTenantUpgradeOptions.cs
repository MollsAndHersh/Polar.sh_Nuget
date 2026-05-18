namespace PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb.Upgrade;

/// <summary>
/// Cosmos-specific knobs for the single-tenant -> multi-tenant upgrade migrator.
/// </summary>
/// <remarks>
/// <para>
/// Cosmos charges Request Units (RUs) for every document read, write, and replace. Unlike
/// the relational providers, a Cosmos backfill is therefore not free: replacing every
/// tenant-naive item in every container can consume tens of thousands of RUs even for
/// modest data sets, and that consumption shows up directly on the operator's bill. To
/// avoid surprising operators with a large unprompted Cosmos charge, the migrator computes
/// an estimated RU cost during a dry-run pass and refuses to run when the estimate exceeds
/// <see cref="AbortIfEstimatedRuCostExceeds"/> unless <see cref="AcknowledgeCosmosRuCost"/>
/// is set to <see langword="true"/>.
/// </para>
/// <para>
/// Bound from the appsettings section
/// <c>PolarSharp:MultiTenant:SingleTenantUpgrade:Cosmos</c>.
/// </para>
/// </remarks>
public sealed class CosmosDbSingleTenantUpgradeOptions
{
    /// <summary>The appsettings section this options class binds to.</summary>
    public const string SectionName = "PolarSharp:MultiTenant:SingleTenantUpgrade:Cosmos";

    /// <summary>
    /// Gets or sets the RU-cost threshold above which the migrator refuses to run unless
    /// <see cref="AcknowledgeCosmosRuCost"/> is also <see langword="true"/>.
    /// </summary>
    /// <value>
    /// Default <c>10000</c> RUs. The migrator estimates total cost as
    /// <c>(item count) * (per-item replace cost)</c> using the per-container per-item replace
    /// RU charge reported by the Cosmos SDK for the first replaced item.
    /// </value>
    public int AbortIfEstimatedRuCostExceeds { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets a value indicating whether the operator has explicitly acknowledged the
    /// estimated RU cost. When <see langword="true"/>, the migrator runs even if the
    /// estimate exceeds <see cref="AbortIfEstimatedRuCostExceeds"/>.
    /// </summary>
    /// <value>
    /// Default <see langword="false"/>. Set via configuration
    /// (<c>PolarSharp:MultiTenant:SingleTenantUpgrade:Cosmos:AcknowledgeCosmosRuCost</c>) or
    /// via the upcoming <c>dotnet polar-mt upgrade --force</c> CLI flag.
    /// </value>
    public bool AcknowledgeCosmosRuCost { get; set; }
}
