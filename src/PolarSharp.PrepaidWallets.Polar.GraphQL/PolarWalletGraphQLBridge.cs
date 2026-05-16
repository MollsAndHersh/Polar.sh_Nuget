using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.Polar.GraphQL;

/// <summary>
/// Polar bridge extending the v1.3 PolarSharp.Reporting.GraphQL schema with wallet types:
/// WalletBalance, WalletHistoryEntry, FundingSource, CustomerPurchaseHistory,
/// PurchaseOrderProgress. Single GraphQL request can return wallet data alongside
/// catalog + reporting types.
/// </summary>
/// <remarks>
/// Phase 22 ships the bridge package shell; full GraphQL type extension lands in Phase 22.x.
/// </remarks>
public static class PolarWalletGraphQLBridgeExtensions
{
    /// <summary>Registers the wallet GraphQL types as extensions to the existing reporting schema.</summary>
    /// <param name="builder">The Hot Chocolate request executor builder.</param>
    /// <returns>The same builder for chaining.</returns>
    public static IRequestExecutorBuilder AddPolarWalletGraphQL(this IRequestExecutorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
