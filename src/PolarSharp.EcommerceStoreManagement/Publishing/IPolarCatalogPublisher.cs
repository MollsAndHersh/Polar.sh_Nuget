namespace PolarSharp.EcommerceStoreManagement.Publishing;

/// <summary>
/// Orchestrates the local-to-Polar publish workflow. Idempotent and resumable: each local
/// entity carries a <c>PolarXxxId</c> that's populated on first publish and used to PATCH on
/// subsequent calls.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Dependency order:</strong> the publisher walks Benefits → Products (with variant
/// + tier expansion) → Discounts → Checkout links so foreign-key references resolve in the
/// correct order in Polar.
/// </para>
/// <para>
/// <strong>Partial failures resume.</strong> An entity that errors mid-batch is marked
/// <see cref="PublishStatus.PublishFailed"/> with the error captured to the audit log; the
/// next publish call picks up where the previous left off.
/// </para>
/// </remarks>
public interface IPolarCatalogPublisher
{
    /// <summary>
    /// Computes the publish plan WITHOUT executing it. Useful for showing a "you're about to
    /// create N products, update M, archive K" preview in admin UI.
    /// </summary>
    Task<Result<PublishPlan, PublishError>> PreviewAsync(PublishOptions options, CancellationToken ct = default);

    /// <summary>Executes a publish. Returns a per-entity outcome report.</summary>
    Task<Result<PublishReport, PublishError>> PublishAsync(PublishOptions options, CancellationToken ct = default);
}

/// <summary>Controls which entities a publish call considers.</summary>
public sealed record PublishOptions
{
    /// <summary>When true, the publisher executes the orchestration WITHOUT writing to Polar. Returns a populated <see cref="PublishReport"/> with simulated successes.</summary>
    public bool DryRun { get; init; }

    /// <summary>Which subset of local entities to publish.</summary>
    public PublishScope Scope { get; init; } = PublishScope.AllDirty;

    /// <summary>When <see cref="Scope"/> is <see cref="PublishScope.SingleProduct"/>, the specific product to publish.</summary>
    public ProductId? SingleProductId { get; init; }

    /// <summary>When true, the publisher invokes the registered translator on each translatable entity before publishing.</summary>
    public bool TranslateOnPublish { get; init; }
}

/// <summary>Which subset of local entities a publish call should consider.</summary>
public enum PublishScope
{
    /// <summary>Every entity with <see cref="PublishStatus.Draft"/> or <see cref="PublishStatus.OutOfSync"/>.</summary>
    AllDirty,
    /// <summary>Only one product (and its dependencies).</summary>
    SingleProduct,
    /// <summary>Every entity, regardless of status. Forces a full re-sync against Polar.</summary>
    Everything,
    /// <summary>Every entity marked <c>IsFakeData = true</c>. Used by the data-seeding toggle.</summary>
    AllFakeData,
}

/// <summary>The set of operations a publish call WOULD perform — the dry-run output.</summary>
public sealed record PublishPlan
{
    /// <summary>Each planned action against Polar, in dependency order.</summary>
    public required IReadOnlyList<PlannedAction> Actions { get; init; }

    /// <summary>How many new Polar entities will be created.</summary>
    public required int CreateCount { get; init; }

    /// <summary>How many existing Polar entities will be PATCHed.</summary>
    public required int UpdateCount { get; init; }

    /// <summary>How many Polar entities will be archived (<c>is_archived: true</c>).</summary>
    public required int ArchiveCount { get; init; }
}

/// <summary>Sealed hierarchy of planned actions — ZH rule 3 (polymorphism over branching).</summary>
public abstract record PlannedAction;

/// <summary>Create a new Polar product (with its variants if any).</summary>
public sealed record CreatePolarProduct(LocalProduct Local) : PlannedAction;

/// <summary>Update an existing Polar product.</summary>
public sealed record UpdatePolarProduct(LocalProduct Local, string ExistingPolarId) : PlannedAction;

/// <summary>Archive an existing Polar product.</summary>
public sealed record ArchivePolarProduct(string PolarId, string Reason) : PlannedAction;

/// <summary>Create a new Polar benefit.</summary>
public sealed record CreatePolarBenefit(LocalBenefit Local) : PlannedAction;

/// <summary>Update an existing Polar benefit.</summary>
public sealed record UpdatePolarBenefit(LocalBenefit Local, string ExistingPolarId) : PlannedAction;

/// <summary>Create a new Polar discount.</summary>
public sealed record CreatePolarDiscount(LocalDiscount Local) : PlannedAction;

/// <summary>Update an existing Polar discount.</summary>
public sealed record UpdatePolarDiscount(LocalDiscount Local, string ExistingPolarId) : PlannedAction;

/// <summary>Create a new Polar checkout link.</summary>
public sealed record CreatePolarCheckoutLink(LocalCheckoutLinkConfig Local) : PlannedAction;

/// <summary>The per-entity outcome report produced by an executed publish.</summary>
public sealed record PublishReport
{
    /// <summary>Per-entity outcomes, in the order they were attempted.</summary>
    public required IReadOnlyList<PublishOutcome> Outcomes { get; init; }

    /// <summary>How long the publish took.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>How many entities published successfully.</summary>
    public required int SucceededCount { get; init; }

    /// <summary>How many entities failed.</summary>
    public required int FailedCount { get; init; }
}

/// <summary>Sealed hierarchy of per-entity outcomes — ZH rule 3.</summary>
public abstract record PublishOutcome(string EntityType, Guid LocalId);

/// <summary>The entity published successfully and is now mirrored in Polar.</summary>
public sealed record PublishedSuccessfully(string EntityType, Guid LocalId, string PolarId) : PublishOutcome(EntityType, LocalId);

/// <summary>The publish was a dry-run — no Polar mutation occurred.</summary>
public sealed record DryRunSimulated(string EntityType, Guid LocalId, string PlannedAction) : PublishOutcome(EntityType, LocalId);

/// <summary>The entity failed to publish.</summary>
public sealed record PublishFailedOutcome(string EntityType, Guid LocalId, PublishError Error) : PublishOutcome(EntityType, LocalId);

/// <summary>Recoverable publish-flow failure.</summary>
public sealed record PublishError(PublishErrorKind Kind, string Message);

/// <summary>Discriminator for publish errors.</summary>
public enum PublishErrorKind
{
    /// <summary>Polar rejected the payload's shape.</summary>
    PolarValidation,
    /// <summary>A dependency (benefit, product) wasn't published yet.</summary>
    DependencyNotPublished,
    /// <summary>Polar API failure (5xx, timeout).</summary>
    PolarApiFailure,
    /// <summary>The local repository write failed (e.g. couldn't persist the captured PolarId).</summary>
    LocalPersistenceFailed,
}
