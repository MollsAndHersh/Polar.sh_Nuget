using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp;

/// <summary>
/// A fluent builder returned by <c>AddPolarInfrastructure</c> that optional packages
/// (<c>PolarSharp.Webhooks</c>, <c>PolarSharp.MultiTenant</c>) extend via their own
/// <c>AddPolar*</c> extension methods.
/// </summary>
/// <remarks>
/// Example full-stack registration:
/// <code>
/// builder.Services
///     .AddPolarInfrastructure(builder.Configuration)
///     .AddPolarWebhooks()
///     .AddPolarMultiTenant();
/// </code>
/// </remarks>
public sealed class PolarInfrastructureBuilder(
    IServiceCollection services,
    IConfiguration configuration)
{
    /// <summary>Gets the <see cref="IServiceCollection"/> for adding services.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>Gets the application <see cref="IConfiguration"/>.</summary>
    public IConfiguration Configuration { get; } = configuration;
}
