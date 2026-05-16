using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.Reporting.GraphQL;

/// <summary>
/// Hot Chocolate GraphQL schema registration for the PolarSharp Reporting read-side.
/// </summary>
/// <remarks>
/// <para>
/// Exposes the reporting drilldown (Customer → Orders → Order detail with line items + refunds
/// + benefit grants) plus aggregate KPI queries (Transactions / Subscriptions / Orders /
/// ErrorAudit / Customers / CustomerEntitlements) as a GraphQL schema that hosts can mount
/// alongside their existing REST endpoints. Hosts can mount the GraphQL endpoint at
/// <c>/graphql/reporting</c> (or any path of their choosing) and serve it to Strawberry Shake
/// typed clients OR Banana Cake Pop interactive UI in Development.
/// </para>
/// <para>
/// <strong>Phase 18 ships the registration scaffold</strong>; the full Query / Mutation type
/// definitions + DataLoaders for N+1 prevention + field-level <c>[RequirePolarPermission]</c>
/// integration + schema-snapshot CI gate land in Phase 18.x. The scaffolded extension is
/// shape-compatible with Hot Chocolate 15.x so subsequent phases just add resolvers without
/// rewriting the registration.
/// </para>
/// </remarks>
public static class ReportingGraphQLBuilderExtensions
{
    /// <summary>
    /// Registers Hot Chocolate GraphQL services for the PolarSharp Reporting schema.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>An <see cref="IRequestExecutorBuilder"/> for further GraphQL configuration.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarReporting()
    ///     .UsePostgreSqlReporting(connStr);
    /// builder.Services.AddPolarReportingGraphQL();
    /// // ...
    /// app.MapGraphQL("/graphql/reporting");
    /// </code>
    /// </example>
    public static IRequestExecutorBuilder AddPolarReportingGraphQL(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddGraphQLServer()
            .AddQueryType(d => d.Name("PolarReportingQuery"))
            // Resolvers + DataLoaders + field-level authz integration land in Phase 18.x.
            // The Query type is registered with no fields for now so the schema compiles;
            // subsequent phases add: transactions, subscriptions, orders, errorAudit, customers,
            // customerEntitlements, customers(paged), customer(id) → orders → orderDetail drilldown.
            .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = false);
    }
}
