using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStorefronts.Abstractions.Pipelines;
using PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling.Stages;

namespace PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling.Extensions;

/// <summary>
/// DI registration helpers for the subscription-billing pipeline.
/// </summary>
/// <remarks>
/// Mirrors <c>OrderProcessingServiceCollectionExtensions</c> in shape — call
/// <see cref="AddPolarSubscriptionBillingPipeline"/> to get the orchestrator + all 5
/// defaults, <see cref="ReplaceStage{TOld, TNew}"/> for tenant overrides, and
/// <see cref="RemoveStage{TStage}"/> to drop a stage entirely.
/// </remarks>
public static class SubscriptionBillingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SubscriptionBillingPipeline"/> + every default
    /// <see cref="ISubscriptionBillingStage"/> as a scoped service.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPolarSubscriptionBillingPipeline(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<SubscriptionBillingPipeline>();

        services.AddScoped<ISubscriptionBillingStage, ValidateSubscriptionStage>();
        services.AddScoped<ISubscriptionBillingStage, CheckPaymentMethodStage>();
        services.AddScoped<ISubscriptionBillingStage, ApplyProrationStage>();
        services.AddScoped<ISubscriptionBillingStage, CapturePaymentStage>();
        services.AddScoped<ISubscriptionBillingStage, NotifyStage>();

        return services;
    }

    /// <summary>
    /// Removes the registration for <typeparamref name="TOld"/> and replaces it with
    /// <typeparamref name="TNew"/>.
    /// </summary>
    /// <typeparam name="TOld">The default stage to remove.</typeparam>
    /// <typeparam name="TNew">The host-supplied replacement stage.</typeparam>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection ReplaceStage<TOld, TNew>(this IServiceCollection services)
        where TOld : class, ISubscriptionBillingStage
        where TNew : class, ISubscriptionBillingStage
    {
        ArgumentNullException.ThrowIfNull(services);

        RemoveStageRegistration<TOld>(services);
        services.AddScoped<ISubscriptionBillingStage, TNew>();
        return services;
    }

    /// <summary>
    /// Removes the registration for <typeparamref name="TStage"/> so the pipeline no
    /// longer dispatches to it.
    /// </summary>
    /// <typeparam name="TStage">The stage type to remove.</typeparam>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection RemoveStage<TStage>(this IServiceCollection services)
        where TStage : class, ISubscriptionBillingStage
    {
        ArgumentNullException.ThrowIfNull(services);
        RemoveStageRegistration<TStage>(services);
        return services;
    }

    private static void RemoveStageRegistration<TStage>(IServiceCollection services)
        where TStage : class, ISubscriptionBillingStage
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(ISubscriptionBillingStage)
                && descriptor.ImplementationType == typeof(TStage))
            {
                services.RemoveAt(i);
            }
        }
    }
}
