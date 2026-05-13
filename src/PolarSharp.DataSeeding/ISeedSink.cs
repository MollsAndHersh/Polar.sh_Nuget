using PolarSharp.EcommerceStoreManagement;

namespace PolarSharp.DataSeeding;

/// <summary>
/// Sink interface — the seeder hands off generated records to whichever persistence layer
/// the host has wired (typically <c>PolarSharp.EcommerceStoreManagement.EntityFrameworkCore</c>'s
/// catalog DbContext, but tests / simple scripts can register a no-op sink and inspect the
/// generated records directly).
/// </summary>
/// <remarks>
/// Each method receives a batch — the orchestrator emits records in one batch per
/// generator-run so the sink can wrap the write in a single transaction.
/// </remarks>
public interface ISeedSink
{
    /// <summary>Persists a batch of products.</summary>
    Task PersistProductsAsync(IReadOnlyList<LocalProduct> products, CancellationToken ct = default);

    /// <summary>Persists a batch of categories.</summary>
    Task PersistCategoriesAsync(IReadOnlyList<LocalCategory> categories, CancellationToken ct = default);

    /// <summary>Persists a batch of departments.</summary>
    Task PersistDepartmentsAsync(IReadOnlyList<LocalDepartment> departments, CancellationToken ct = default);

    /// <summary>Persists a batch of benefits.</summary>
    Task PersistBenefitsAsync(IReadOnlyList<LocalBenefit> benefits, CancellationToken ct = default);

    /// <summary>Persists a batch of discounts.</summary>
    Task PersistDiscountsAsync(IReadOnlyList<LocalDiscount> discounts, CancellationToken ct = default);

    /// <summary>Persists a batch of checkout links.</summary>
    Task PersistCheckoutLinksAsync(IReadOnlyList<LocalCheckoutLinkConfig> links, CancellationToken ct = default);

    /// <summary>Deletes every record where <c>IsFakeData = true</c> for the current tenant. Returns the count deleted.</summary>
    Task<int> DeleteAllFakeDataAsync(CancellationToken ct = default);
}

/// <summary>
/// No-op default sink — useful for tests or hosts that want to inspect generated records
/// before persistence. Records counts so callers can verify how many would have been
/// persisted.
/// </summary>
public sealed class CountingNoOpSeedSink : ISeedSink
{
    /// <summary>Cumulative count of records the sink has "persisted" since construction.</summary>
    public int TotalPersisted { get; private set; }

    /// <summary>Cumulative count of records the sink has "deleted" since construction.</summary>
    public int TotalDeleted { get; private set; }

    /// <inheritdoc/>
    public Task PersistProductsAsync(IReadOnlyList<LocalProduct> products, CancellationToken ct = default)
    { TotalPersisted += products?.Count ?? 0; return Task.CompletedTask; }

    /// <inheritdoc/>
    public Task PersistCategoriesAsync(IReadOnlyList<LocalCategory> categories, CancellationToken ct = default)
    { TotalPersisted += categories?.Count ?? 0; return Task.CompletedTask; }

    /// <inheritdoc/>
    public Task PersistDepartmentsAsync(IReadOnlyList<LocalDepartment> departments, CancellationToken ct = default)
    { TotalPersisted += departments?.Count ?? 0; return Task.CompletedTask; }

    /// <inheritdoc/>
    public Task PersistBenefitsAsync(IReadOnlyList<LocalBenefit> benefits, CancellationToken ct = default)
    { TotalPersisted += benefits?.Count ?? 0; return Task.CompletedTask; }

    /// <inheritdoc/>
    public Task PersistDiscountsAsync(IReadOnlyList<LocalDiscount> discounts, CancellationToken ct = default)
    { TotalPersisted += discounts?.Count ?? 0; return Task.CompletedTask; }

    /// <inheritdoc/>
    public Task PersistCheckoutLinksAsync(IReadOnlyList<LocalCheckoutLinkConfig> links, CancellationToken ct = default)
    { TotalPersisted += links?.Count ?? 0; return Task.CompletedTask; }

    /// <inheritdoc/>
    public Task<int> DeleteAllFakeDataAsync(CancellationToken ct = default)
    {
        var deleted = TotalPersisted;
        TotalDeleted += deleted;
        TotalPersisted = 0;
        return Task.FromResult(deleted);
    }
}
