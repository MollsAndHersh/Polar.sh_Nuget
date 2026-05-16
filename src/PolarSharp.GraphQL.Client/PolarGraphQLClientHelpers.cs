namespace PolarSharp.GraphQL.Client;

/// <summary>
/// Strawberry Shake codegen helper + bundled schema files for typed .NET GraphQL clients
/// targeting the PolarSharp Reporting + Catalog read-side schemas.
/// </summary>
/// <remarks>
/// <para>
/// This package contains the bundled GraphQL schema definitions (introspection JSON +
/// SDL form) that Strawberry Shake's <c>dotnet graphql generate</c> command consumes
/// to produce strongly-typed client classes. Hosts who want a typed GraphQL client for
/// PolarSharp's reporting or catalog APIs reference this package + run Strawberry Shake's
/// codegen against the bundled schemas; they get typed <c>IReportingGraphQLClient</c>
/// and <c>ICatalogGraphQLClient</c> interfaces ready to inject.
/// </para>
/// <para>
/// <strong>Phase 18 ships the package shell</strong>; the bundled schema files (introspection
/// JSON) for each GraphQL endpoint + the <c>dotnet new polar-graphql-client</c> template
/// scaffold + the explicit Strawberry Shake codegen configuration helper land in Phase 18.x
/// once the Reporting + Catalog schemas have stable field sets.
/// </para>
/// </remarks>
public static class PolarGraphQLClientHelpers
{
    /// <summary>
    /// Returns the package version. Used to verify the bundled schema files match the
    /// expected PolarSharp GraphQL endpoint version at codegen time.
    /// </summary>
    public static string PackageVersion => "1.0.0";
}
