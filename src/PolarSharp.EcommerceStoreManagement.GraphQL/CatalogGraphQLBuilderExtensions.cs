using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.EcommerceStoreManagement.GraphQL;

/// <summary>
/// Hot Chocolate GraphQL schema registration for the PolarSharp catalog read-side.
/// </summary>
/// <remarks>
/// <para>
/// Exposes the local catalog (products, variants, categories, departments, discounts,
/// checkout links, tier groups, business profile) as a GraphQL schema with localized
/// field resolution. The schema is READ-ONLY by design — catalog mutations stay on
/// REST endpoints + the publisher orchestrator, so the GraphQL surface doesn't need
/// to mirror the publish workflow's complex idempotency semantics.
/// </para>
/// <para>
/// Localized field resolution: <c>product(id, language) { name description }</c> routes
/// through <c>IPolarCatalogReader</c> which integrates with the v1.2 translation cache
/// (warm-on-read pre-warm preserves cache-hot reads). Tenants can request products in
/// any language they've configured translations for.
/// </para>
/// <para>
/// <strong>Phase 18 ships the registration scaffold</strong>; the full Query type definitions
/// + DataLoaders + field-level <c>[RequirePolarPermission]</c> integration + schema-snapshot
/// CI gate land in Phase 18.x.
/// </para>
/// </remarks>
public static class CatalogGraphQLBuilderExtensions
{
    /// <summary>
    /// Registers Hot Chocolate GraphQL services for the PolarSharp catalog read-side schema.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>An <see cref="IRequestExecutorBuilder"/> for further GraphQL configuration.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarEcommerce()
    ///     .UseSqlServer(connStr);
    /// builder.Services.AddPolarCatalogGraphQL();
    /// // ...
    /// app.MapGraphQL("/graphql/catalog");
    /// </code>
    /// </example>
    public static IRequestExecutorBuilder AddPolarCatalogGraphQL(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddGraphQLServer()
            .AddQueryType(d => d.Name("PolarCatalogQuery"))
            // Resolvers land in Phase 18.x: products(page, search, language), product(id, language),
            // categories(language), category(id, language), discounts, businessProfile, etc.
            .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = false);
    }
}
