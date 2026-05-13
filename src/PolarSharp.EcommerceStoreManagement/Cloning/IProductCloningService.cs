namespace PolarSharp.EcommerceStoreManagement.Cloning;

/// <summary>
/// Clones a <see cref="LocalProduct"/> within the current tenant. Resets all Polar-side
/// state (PolarProductId, LastPublishedAt, Status) so the clone is a fresh Draft. Generates
/// new <see cref="ProductId"/> and <see cref="VariantId"/> values for the new product and
/// every variant in its tree.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Duplicate prevention:</strong> the new <c>MasterName</c> is auto-suffixed with
/// <c>" (Copy)"</c> (then <c>" (Copy 2)"</c>, <c>" (Copy 3)"</c> ... up to 100) to avoid
/// the <c>(tenant_id, master_name)</c> unique-index collision. Caller-supplied overrides
/// take precedence over the auto-suffix — but are themselves checked against the same
/// unique index before insert.
/// </para>
/// <para>
/// <strong>Cascade defaults:</strong> by default the clone includes every variant, every
/// category assignment, every attached benefit, and every translation row. Use
/// <see cref="CloneProductOptions"/> to opt out of any cascade — e.g. cloning a product as
/// an English-only starting point for a new market.
/// </para>
/// </remarks>
public interface IProductCloningService
{
    /// <summary>Clones the product. All work happens in a single transaction; a partial clone never persists.</summary>
    /// <param name="source">The product id to clone.</param>
    /// <param name="overrides">Optional field overrides (name, description, price, category list). Pass <see langword="null"/> to use auto-defaults.</param>
    /// <param name="options">Optional cascade toggles. Pass <see langword="null"/> to include everything.</param>
    /// <param name="ct">Cancellation.</param>
    Task<Result<LocalProduct, CloningError>> CloneAsync(
        ProductId source,
        CloneProductOverrides? overrides = null,
        CloneProductOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>Field overrides applied on top of the cloned source. Any field left <see langword="null"/> uses the source value (with the name suffixed automatically to avoid collisions).</summary>
public sealed record CloneProductOverrides
{
    /// <summary>Override the new product's master name. When <see langword="null"/>, the source name is auto-suffixed with <c>" (Copy)"</c> / <c>" (Copy 2)"</c> / etc.</summary>
    public string? NewMasterName { get; init; }
    /// <summary>Override the new product's master description.</summary>
    public string? NewMasterDescription { get; init; }
    /// <summary>Replace the price with a different one.</summary>
    public LocalPrice? NewPrice { get; init; }
    /// <summary>Replace the M:N category assignments with the supplied list (empty list = uncategorise).</summary>
    public IReadOnlyList<CategoryId>? NewCategoryIds { get; init; }
    /// <summary>Merge into the cloned <see cref="PolarSharp.BaseEntities.PolarProductBase.Metadata"/> dictionary.</summary>
    public IReadOnlyDictionary<string, string>? MetadataOverrides { get; init; }
}

/// <summary>Cascade toggles — which related rows to copy along with the product.</summary>
public sealed record CloneProductOptions
{
    /// <summary>When <see langword="true"/>, every <see cref="LocalProductVariant"/> on the source is cloned with a fresh <see cref="VariantId"/>. Default <see langword="true"/>.</summary>
    public bool IncludeVariants { get; init; } = true;
    /// <summary>When <see langword="true"/>, every product↔category M:N row is re-inserted for the new product id. Default <see langword="true"/>.</summary>
    public bool IncludeCategoryAssignments { get; init; } = true;
    /// <summary>When <see langword="true"/>, every translation row is duplicated with the new entity id. Default <see langword="true"/>.</summary>
    public bool IncludeTranslations { get; init; } = true;
    /// <summary>When <see langword="true"/>, the <see cref="LocalProduct.AttachedBenefits"/> list is copied. Default <see langword="true"/>.</summary>
    public bool IncludeAttachedBenefits { get; init; } = true;
}
