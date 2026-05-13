namespace PolarSharp.EcommerceStoreManagement;

/// <summary>Strongly-typed identifier for a local catalog product.</summary>
public readonly record struct ProductId(Guid Value)
{
    /// <summary>Creates a new random product identifier.</summary>
    public static ProductId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a local product variant.</summary>
public readonly record struct VariantId(Guid Value)
{
    /// <summary>Creates a new random variant identifier.</summary>
    public static VariantId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a catalog category.</summary>
public readonly record struct CategoryId(Guid Value)
{
    /// <summary>Creates a new random category identifier.</summary>
    public static CategoryId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a catalog department (top-level grouping).</summary>
public readonly record struct DepartmentId(Guid Value)
{
    /// <summary>Creates a new random department identifier.</summary>
    public static DepartmentId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a tier-group definition.</summary>
public readonly record struct TierGroupId(Guid Value)
{
    /// <summary>Creates a new random tier-group identifier.</summary>
    public static TierGroupId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a local benefit definition.</summary>
public readonly record struct BenefitId(Guid Value)
{
    /// <summary>Creates a new random benefit identifier.</summary>
    public static BenefitId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a local discount definition.</summary>
public readonly record struct DiscountId(Guid Value)
{
    /// <summary>Creates a new random discount identifier.</summary>
    public static DiscountId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a checkout-link configuration.</summary>
public readonly record struct CheckoutLinkId(Guid Value)
{
    /// <summary>Creates a new random checkout-link identifier.</summary>
    public static CheckoutLinkId New() => new(Guid.NewGuid());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
