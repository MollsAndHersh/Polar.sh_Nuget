namespace PolarSharp.EcommerceStoreManagement.Persistence;

/// <summary>
/// Top-level catalog persistence facade. Composes the per-entity repositories so consumers
/// can inject one service and reach every catalog table.
/// </summary>
public interface ICatalogRepository
{
    /// <summary>Products and variants.</summary>
    IProductRepository Products { get; }

    /// <summary>Categories.</summary>
    ICategoryRepository Categories { get; }

    /// <summary>Departments.</summary>
    IDepartmentRepository Departments { get; }

    /// <summary>Tier-group ladders.</summary>
    ITierGroupRepository TierGroups { get; }

    /// <summary>Local benefit definitions.</summary>
    IBenefitRepository Benefits { get; }

    /// <summary>Local discount definitions.</summary>
    IDiscountRepository Discounts { get; }

    /// <summary>Checkout-link configurations.</summary>
    ICheckoutLinkRepository CheckoutLinks { get; }

    /// <summary>The audit log.</summary>
    IAdminAuditLogReader AuditLog { get; }
}

/// <summary>CRUD over <see cref="LocalProduct"/>.</summary>
public interface IProductRepository
{
    /// <summary>Returns a product by id, or <see langword="null"/> when missing.</summary>
    Task<LocalProduct?> GetAsync(ProductId id, CancellationToken ct = default);
    /// <summary>Returns every product in the current tenant.</summary>
    Task<IReadOnlyList<LocalProduct>> ListAsync(CancellationToken ct = default);
    /// <summary>Returns every product assigned to the supplied category.</summary>
    Task<IReadOnlyList<LocalProduct>> ListByCategoryAsync(CategoryId categoryId, CancellationToken ct = default);
    /// <summary>Persists a new product.</summary>
    Task AddAsync(LocalProduct product, CancellationToken ct = default);
    /// <summary>Updates an existing product.</summary>
    Task UpdateAsync(LocalProduct product, CancellationToken ct = default);
    /// <summary>Deletes the product.</summary>
    Task DeleteAsync(ProductId id, CancellationToken ct = default);
}

/// <summary>CRUD over <see cref="LocalCategory"/>.</summary>
public interface ICategoryRepository
{
    /// <summary>Returns a category by id.</summary>
    Task<LocalCategory?> GetAsync(CategoryId id, CancellationToken ct = default);
    /// <summary>Returns every category in the current tenant.</summary>
    Task<IReadOnlyList<LocalCategory>> ListAsync(CancellationToken ct = default);
    /// <summary>Returns every category that has the supplied parent (or root categories when null).</summary>
    Task<IReadOnlyList<LocalCategory>> ListChildrenAsync(CategoryId? parentId, CancellationToken ct = default);
    /// <summary>Persists a new category.</summary>
    Task AddAsync(LocalCategory category, CancellationToken ct = default);
    /// <summary>Updates an existing category.</summary>
    Task UpdateAsync(LocalCategory category, CancellationToken ct = default);
    /// <summary>Deletes the category. Cascades to remove the product↔category assignments.</summary>
    Task DeleteAsync(CategoryId id, CancellationToken ct = default);
}

/// <summary>CRUD over <see cref="LocalDepartment"/>.</summary>
public interface IDepartmentRepository
{
    /// <summary>Returns a department by id.</summary>
    Task<LocalDepartment?> GetAsync(DepartmentId id, CancellationToken ct = default);
    /// <summary>Returns every department in the current tenant.</summary>
    Task<IReadOnlyList<LocalDepartment>> ListAsync(CancellationToken ct = default);
    /// <summary>Persists a new department.</summary>
    Task AddAsync(LocalDepartment department, CancellationToken ct = default);
    /// <summary>Updates an existing department.</summary>
    Task UpdateAsync(LocalDepartment department, CancellationToken ct = default);
    /// <summary>Deletes the department.</summary>
    Task DeleteAsync(DepartmentId id, CancellationToken ct = default);
}

/// <summary>CRUD over <see cref="LocalTierGroup"/>.</summary>
public interface ITierGroupRepository
{
    /// <summary>Returns a tier-group by id.</summary>
    Task<LocalTierGroup?> GetAsync(TierGroupId id, CancellationToken ct = default);
    /// <summary>Returns every tier-group in the current tenant.</summary>
    Task<IReadOnlyList<LocalTierGroup>> ListAsync(CancellationToken ct = default);
    /// <summary>Persists a new tier-group.</summary>
    Task AddAsync(LocalTierGroup tierGroup, CancellationToken ct = default);
    /// <summary>Updates an existing tier-group.</summary>
    Task UpdateAsync(LocalTierGroup tierGroup, CancellationToken ct = default);
    /// <summary>Deletes the tier-group.</summary>
    Task DeleteAsync(TierGroupId id, CancellationToken ct = default);
}

/// <summary>CRUD over <see cref="LocalBenefit"/>.</summary>
public interface IBenefitRepository
{
    /// <summary>Returns a benefit by id.</summary>
    Task<LocalBenefit?> GetAsync(BenefitId id, CancellationToken ct = default);
    /// <summary>Returns every benefit in the current tenant.</summary>
    Task<IReadOnlyList<LocalBenefit>> ListAsync(CancellationToken ct = default);
    /// <summary>Persists a new benefit.</summary>
    Task AddAsync(LocalBenefit benefit, CancellationToken ct = default);
    /// <summary>Updates an existing benefit.</summary>
    Task UpdateAsync(LocalBenefit benefit, CancellationToken ct = default);
    /// <summary>Deletes the benefit.</summary>
    Task DeleteAsync(BenefitId id, CancellationToken ct = default);
}

/// <summary>CRUD over <see cref="LocalDiscount"/>.</summary>
public interface IDiscountRepository
{
    /// <summary>Returns a discount by id.</summary>
    Task<LocalDiscount?> GetAsync(DiscountId id, CancellationToken ct = default);
    /// <summary>Returns every discount in the current tenant.</summary>
    Task<IReadOnlyList<LocalDiscount>> ListAsync(CancellationToken ct = default);
    /// <summary>Persists a new discount.</summary>
    Task AddAsync(LocalDiscount discount, CancellationToken ct = default);
    /// <summary>Updates an existing discount.</summary>
    Task UpdateAsync(LocalDiscount discount, CancellationToken ct = default);
    /// <summary>Deletes the discount.</summary>
    Task DeleteAsync(DiscountId id, CancellationToken ct = default);
}

/// <summary>CRUD over <see cref="LocalCheckoutLinkConfig"/>.</summary>
public interface ICheckoutLinkRepository
{
    /// <summary>Returns a checkout link by id.</summary>
    Task<LocalCheckoutLinkConfig?> GetAsync(CheckoutLinkId id, CancellationToken ct = default);
    /// <summary>Returns every checkout link in the current tenant.</summary>
    Task<IReadOnlyList<LocalCheckoutLinkConfig>> ListAsync(CancellationToken ct = default);
    /// <summary>Persists a new checkout link.</summary>
    Task AddAsync(LocalCheckoutLinkConfig link, CancellationToken ct = default);
    /// <summary>Updates an existing checkout link.</summary>
    Task UpdateAsync(LocalCheckoutLinkConfig link, CancellationToken ct = default);
    /// <summary>Deletes the checkout link.</summary>
    Task DeleteAsync(CheckoutLinkId id, CancellationToken ct = default);
}

/// <summary>Read access to the admin audit log.</summary>
public interface IAdminAuditLogReader
{
    /// <summary>Returns entries within the supplied date range, newest first. Paged.</summary>
    Task<IReadOnlyList<AdminAuditLogEntry>> ListAsync(
        DateTimeOffset? since,
        DateTimeOffset? until,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default);

    /// <summary>Returns entries affecting a specific entity, oldest first.</summary>
    Task<IReadOnlyList<AdminAuditLogEntry>> ListForEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default);
}
