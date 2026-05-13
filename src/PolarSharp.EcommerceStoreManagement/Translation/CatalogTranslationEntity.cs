using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.Translation;

/// <summary>
/// EF entity backing the <c>catalog_translations</c> table — one row per
/// (tenant, entity_type, entity_id, language, field_name) tuple.
/// </summary>
/// <remarks>
/// Master-language values stay in the source entity row; only non-master translations live
/// here. Reassembly happens in <c>IPolarCatalogReader</c> via the cache + this table.
/// </remarks>
public sealed class CatalogTranslationEntity : ITenantOwned, IFakeDataAware
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant id (Guid string).</summary>
    public string TenantId { get; set; } = "";

    /// <summary>Which entity type this row translates.</summary>
    public CatalogTranslationEntityType EntityType { get; set; }

    /// <summary>The translated entity's identifier.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Target language code (e.g. <c>"es-MX"</c>).</summary>
    public string Language { get; set; } = "";

    /// <summary>The field within the entity (e.g. <c>"name"</c>, <c>"description"</c>).</summary>
    public string FieldName { get; set; } = "";

    /// <summary>The translated value.</summary>
    public string TranslatedValue { get; set; } = "";

    /// <summary>True when the value was produced by AI translation. False when a human edited or curated it.</summary>
    public bool IsMachineTranslated { get; set; } = true;

    /// <summary>The provider that produced the translation (<c>"Anthropic"</c>, <c>"OpenAI"</c>, etc.). <see langword="null"/> for human-curated.</summary>
    public string? SourceProvider { get; set; }

    /// <summary>Model name the provider used.</summary>
    public string? SourceModel { get; set; }

    /// <inheritdoc/>
    public bool IsFakeData { get; set; }

    /// <summary>UTC of the row's creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC of the row's most-recent update.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
