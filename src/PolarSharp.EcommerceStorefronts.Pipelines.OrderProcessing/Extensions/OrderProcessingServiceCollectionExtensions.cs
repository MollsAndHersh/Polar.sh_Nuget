using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;
using PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing.Stages;

namespace PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing.Extensions;

/// <summary>
/// DI registration helpers for the order-processing pipeline.
/// </summary>
/// <remarks>
/// <see cref="AddPolarOrderProcessingPipeline"/> registers the
/// <see cref="OrderProcessingPipeline"/> orchestrator plus every default stage as a
/// scoped service. Hosts that need per-tenant customisation can call
/// <see cref="ReplaceStage{TOld, TNew}"/> to swap a default for their own
/// implementation, or <see cref="RemoveStage{TStage}"/> to drop a stage entirely.
/// New stages can be appended by registering them directly against
/// <see cref="IOrderProcessingStage"/>.
/// </remarks>
public static class OrderProcessingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OrderProcessingPipeline"/> + every default
    /// <see cref="IOrderProcessingStage"/> as a scoped service.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPolarOrderProcessingPipeline(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<OrderProcessingPipeline>();

        services.AddScoped<IOrderProcessingStage, ValidateLineItemsStage>();
        services.AddScoped<IOrderProcessingStage, CheckInventoryStage>();
        services.AddScoped<IOrderProcessingStage, ApplyDiscountsStage>();
        services.AddScoped<IOrderProcessingStage, QuoteTaxStage>();
        services.AddScoped<IOrderProcessingStage, QuoteShippingStage>();
        services.AddScoped<IOrderProcessingStage, CapturePaymentStage>();
        services.AddScoped<IOrderProcessingStage, FulfillStage>();
        services.AddScoped<IOrderProcessingStage, NotifyStage>();

        return services;
    }

    /// <summary>
    /// Removes the registration for <typeparamref name="TOld"/> and replaces it with
    /// <typeparamref name="TNew"/>, preserving the position of the stage in the chain
    /// (the orchestrator sorts by <see cref="IOrderProcessingStage.Order"/>, so the
    /// new stage should expose the same <c>Order</c> value as the old one).
    /// </summary>
    /// <typeparam name="TOld">The default stage to remove.</typeparam>
    /// <typeparam name="TNew">The host-supplied replacement stage.</typeparam>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection ReplaceStage<TOld, TNew>(this IServiceCollection services)
        where TOld : class, IOrderProcessingStage
        where TNew : class, IOrderProcessingStage
    {
        ArgumentNullException.ThrowIfNull(services);

        RemoveStageRegistration<TOld>(services);
        services.AddScoped<IOrderProcessingStage, TNew>();
        return services;
    }

    /// <summary>
    /// Removes the registration for <typeparamref name="TStage"/> so the pipeline no
    /// longer dispatches to it. Useful when a host disables a capability entirely
    /// (e.g. a digital-only storefront removing <c>QuoteShippingStage</c>).
    /// </summary>
    /// <typeparam name="TStage">The stage type to remove.</typeparam>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection RemoveStage<TStage>(this IServiceCollection services)
        where TStage : class, IOrderProcessingStage
    {
        ArgumentNullException.ThrowIfNull(services);
        RemoveStageRegistration<TStage>(services);
        return services;
    }

    private static void RemoveStageRegistration<TStage>(IServiceCollection services)
        where TStage : class, IOrderProcessingStage
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IOrderProcessingStage)
                && descriptor.ImplementationType == typeof(TStage))
            {
                services.RemoveAt(i);
            }
        }
    }
}
