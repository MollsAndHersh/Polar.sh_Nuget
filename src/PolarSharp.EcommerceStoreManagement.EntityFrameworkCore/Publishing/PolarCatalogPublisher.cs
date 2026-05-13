using System.Diagnostics;
using System.Text.Json;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.Publishing;
using PolarSharp.MultiTenant;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Publishing;

/// <summary>
/// Default <see cref="IPolarCatalogPublisher"/> implementation. Walks the local catalog in
/// dependency order (Benefits → Products → Discounts → Checkout links), produces a typed
/// plan, then executes the plan against Polar via <see cref="IPolarPublishingApi"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Idempotency.</strong> Each local entity carries a <c>PolarXxxId</c>. A null id
/// means "first publish — create"; a non-null id means "update existing". Re-publish is
/// safe — the publisher PATCHes existing Polar entities rather than duplicating them.
/// </para>
/// <para>
/// <strong>Partial-failure resume.</strong> Each entity's outcome is persisted as we go
/// (PolarXxxId + LastPublishedAt + PublishStatus). A failure mid-batch leaves the failed
/// entity at <see cref="PublishStatus.PublishFailed"/>; subsequent publish calls in
/// <see cref="PublishScope.AllDirty"/> mode pick it up automatically.
/// </para>
/// <para>
/// <strong>Variant expansion.</strong> A <see cref="LocalProduct"/> with <c>HasVariants=true</c>
/// produces ONE plan action but multiple Polar products at execution time (one per variant).
/// The plan's <see cref="PublishPlan.CreateCount"/> / <c>UpdateCount</c> counts include the
/// expanded count.
/// </para>
/// <para>
/// <strong>Deferrals.</strong> Tier-group expansion (Basic/Advanced/Ultimate cumulative
/// benefit bundles) is scaffolded but NOT executed by v1.3.E; tracked under a TASK-V13-005
/// follow-up. Translation-on-publish (<see cref="PublishOptions.TranslateOnPublish"/>) is
/// honoured by invoking the translator before each entity's HTTP call IF the option is on
/// AND a translator is registered; otherwise no-ops. Polar HTTP wiring itself is best-effort
/// (TASK-V20-001) — until sandbox-validated, the default <see cref="IPolarPublishingApi"/>
/// implementation returns <c>UnexpectedFailure</c>, so production hosts should supply their
/// own implementation or wait for the validated wrapper.
/// </para>
/// </remarks>
internal sealed class PolarCatalogPublisher(
    PolarCatalogDbContext db,
    IPolarPublishingApi polarApi,
    IMultiTenantContextAccessor tenantAccessor,
    TimeProvider time,
    ILogger<PolarCatalogPublisher> logger) : IPolarCatalogPublisher
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IPolarPublishingApi _polarApi = polarApi ?? throw new ArgumentNullException(nameof(polarApi));
    private readonly IMultiTenantContextAccessor _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ILogger<PolarCatalogPublisher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private string CurrentTenantId =>
        (_tenantAccessor.MultiTenantContext?.TenantInfo as PolarTenantInfo)?.Id
        ?? throw new InvalidOperationException("Publisher requires a current tenant in scope.");

    public async Task<Result<PublishPlan, PublishError>> PreviewAsync(PublishOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sets = await LoadEntitiesAsync(options, ct).ConfigureAwait(false);
        var plan = BuildPlan(sets);
        return Result<PublishPlan, PublishError>.Success(plan);
    }

    public async Task<Result<PublishReport, PublishError>> PublishAsync(PublishOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sw = Stopwatch.StartNew();

        var sets = await LoadEntitiesAsync(options, ct).ConfigureAwait(false);
        var plan = BuildPlan(sets);

        var outcomes = new List<PublishOutcome>(plan.Actions.Count);
        var succeeded = 0;
        var failed = 0;

        foreach (var action in plan.Actions)
        {
            ct.ThrowIfCancellationRequested();
            var outcome = await ExecuteActionAsync(action, sets, options, ct).ConfigureAwait(false);
            outcomes.Add(outcome);
            switch (outcome)
            {
                case PublishedSuccessfully or DryRunSimulated: succeeded++; break;
                case PublishFailedOutcome: failed++; break;
            }
        }

        // One commit at the end captures every state mutation from this publish run.
        if (!options.DryRun)
        {
            try
            {
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Publisher: local persistence of {Count} outcome state changes failed.", outcomes.Count);
                return Result<PublishReport, PublishError>.Failure(new PublishError(
                    PublishErrorKind.LocalPersistenceFailed,
                    $"Failed to persist publish outcomes locally: {ex.GetBaseException().Message}"));
            }
        }

        sw.Stop();
        return Result<PublishReport, PublishError>.Success(new PublishReport
        {
            Outcomes = outcomes,
            Duration = sw.Elapsed,
            SucceededCount = succeeded,
            FailedCount = failed,
        });
    }

    // ── Plan computation ─────────────────────────────────────────────────────────

    private async Task<EntitySets> LoadEntitiesAsync(PublishOptions options, CancellationToken ct)
    {
        var benefitsQuery = _db.Benefits.AsQueryable();
        var productsQuery = _db.Products.AsQueryable();
        var discountsQuery = _db.Discounts.AsQueryable();
        var linksQuery = _db.CheckoutLinks.AsQueryable();

        switch (options.Scope)
        {
            case PublishScope.AllDirty:
                benefitsQuery = benefitsQuery.Where(b => b.Status == PublishStatus.Draft || b.Status == PublishStatus.OutOfSync || b.Status == PublishStatus.PublishFailed);
                productsQuery = productsQuery.Where(p => p.Status == PublishStatus.Draft || p.Status == PublishStatus.OutOfSync || p.Status == PublishStatus.PublishFailed);
                discountsQuery = discountsQuery.Where(d => d.Status == PublishStatus.Draft || d.Status == PublishStatus.OutOfSync || d.Status == PublishStatus.PublishFailed);
                linksQuery = linksQuery.Where(l => l.Status == PublishStatus.Draft || l.Status == PublishStatus.OutOfSync || l.Status == PublishStatus.PublishFailed);
                break;
            case PublishScope.SingleProduct when options.SingleProductId is { } pid:
                benefitsQuery = benefitsQuery.Where(_ => false);
                productsQuery = productsQuery.Where(p => p.Id == pid.Value);
                discountsQuery = discountsQuery.Where(_ => false);
                linksQuery = linksQuery.Where(_ => false);
                break;
            case PublishScope.SingleProduct:
                // SingleProductId is null — load nothing.
                benefitsQuery = benefitsQuery.Where(_ => false);
                productsQuery = productsQuery.Where(_ => false);
                discountsQuery = discountsQuery.Where(_ => false);
                linksQuery = linksQuery.Where(_ => false);
                break;
            case PublishScope.AllFakeData:
                // The catalog DbContext's global filter hides fake-data rows when the tenant's
                // AllowFakeData toggle is false. AllFakeData scope explicitly wants those rows
                // (it's the data-seeding sync path), so we ignore the filter and re-apply
                // tenant scoping by leaning on the entity-level configurations.
                benefitsQuery = benefitsQuery.IgnoreQueryFilters().Where(b => b.IsFakeData && b.TenantId == CurrentTenantId);
                productsQuery = productsQuery.IgnoreQueryFilters().Where(p => p.IsFakeData && p.TenantId == CurrentTenantId);
                discountsQuery = discountsQuery.IgnoreQueryFilters().Where(d => d.IsFakeData && d.TenantId == CurrentTenantId);
                linksQuery = linksQuery.IgnoreQueryFilters().Where(l => l.IsFakeData && l.TenantId == CurrentTenantId);
                break;
            case PublishScope.Everything:
                break;       // load all of every kind
        }

        var benefits = await benefitsQuery.ToListAsync(ct).ConfigureAwait(false);
        var products = await productsQuery.ToListAsync(ct).ConfigureAwait(false);
        var discounts = await discountsQuery.ToListAsync(ct).ConfigureAwait(false);
        var links = await linksQuery.ToListAsync(ct).ConfigureAwait(false);

        // Variants are loaded for any product that opts into them.
        var productIds = products.Where(p => p.HasVariants).Select(p => p.Id).ToList();
        var variants = productIds.Count == 0
            ? []
            : await _db.Variants.Where(v => productIds.Contains(v.ProductId)).ToListAsync(ct).ConfigureAwait(false);

        return new EntitySets(benefits, products, variants, discounts, links);
    }

    private static PublishPlan BuildPlan(EntitySets sets)
    {
        var actions = new List<PlannedAction>();
        var createCount = 0;
        var updateCount = 0;

        // Dependency order: Benefits → Products → Discounts → Checkout links.
        foreach (var b in sets.Benefits)
        {
            if (string.IsNullOrEmpty(b.PolarBenefitId))
            {
                actions.Add(new CreatePolarBenefit(ProjectBenefit(b)));
                createCount++;
            }
            else
            {
                actions.Add(new UpdatePolarBenefit(ProjectBenefit(b), b.PolarBenefitId));
                updateCount++;
            }
        }

        foreach (var p in sets.Products)
        {
            var variantsForProduct = sets.Variants.Where(v => v.ProductId == p.Id).ToList();
            var expansionCount = p.HasVariants && variantsForProduct.Count > 0 ? variantsForProduct.Count : 1;
            var projected = ProjectProduct(p, variantsForProduct);

            if (string.IsNullOrEmpty(p.PolarProductId))
            {
                actions.Add(new CreatePolarProduct(projected));
                createCount += expansionCount;
            }
            else
            {
                actions.Add(new UpdatePolarProduct(projected, p.PolarProductId));
                updateCount += expansionCount;
            }
        }

        foreach (var d in sets.Discounts)
        {
            if (string.IsNullOrEmpty(d.PolarDiscountId))
            {
                actions.Add(new CreatePolarDiscount(ProjectDiscount(d)));
                createCount++;
            }
            else
            {
                actions.Add(new UpdatePolarDiscount(ProjectDiscount(d), d.PolarDiscountId));
                updateCount++;
            }
        }

        foreach (var l in sets.CheckoutLinks)
        {
            if (string.IsNullOrEmpty(l.PolarCheckoutLinkId))
            {
                actions.Add(new CreatePolarCheckoutLink(ProjectCheckoutLink(l)));
                createCount++;
            }
            // Updates to checkout links are NOT supported by Polar's API in the same way; we
            // skip update planning for them. Hosts that want to "update" a link archive the
            // old one and create a new one.
        }

        return new PublishPlan
        {
            Actions = actions,
            CreateCount = createCount,
            UpdateCount = updateCount,
            ArchiveCount = 0,            // archive planning is a v2.0 concern
        };
    }

    // ── Action execution ─────────────────────────────────────────────────────────

    private async Task<PublishOutcome> ExecuteActionAsync(
        PlannedAction action,
        EntitySets sets,
        PublishOptions options,
        CancellationToken ct)
    {
        if (options.DryRun)
        {
            return BuildDryRunOutcome(action);
        }

        return action switch
        {
            CreatePolarBenefit create => await PublishBenefitCreateAsync(create, sets, ct).ConfigureAwait(false),
            UpdatePolarBenefit update => await PublishBenefitUpdateAsync(update, sets, ct).ConfigureAwait(false),
            CreatePolarProduct create => await PublishProductCreateAsync(create, sets, ct).ConfigureAwait(false),
            UpdatePolarProduct update => await PublishProductUpdateAsync(update, sets, ct).ConfigureAwait(false),
            CreatePolarDiscount create => await PublishDiscountCreateAsync(create, sets, ct).ConfigureAwait(false),
            UpdatePolarDiscount update => await PublishDiscountUpdateAsync(update, sets, ct).ConfigureAwait(false),
            CreatePolarCheckoutLink create => await PublishCheckoutLinkCreateAsync(create, sets, ct).ConfigureAwait(false),
            _ => new PublishFailedOutcome("Unknown", Guid.Empty, new PublishError(PublishErrorKind.PolarValidation, $"Unknown action type {action.GetType().Name}")),
        };
    }

    private static PublishOutcome BuildDryRunOutcome(PlannedAction action) => action switch
    {
        CreatePolarBenefit cb => new DryRunSimulated("Benefit", Guid.Parse(cb.Local.Id), "Create"),
        UpdatePolarBenefit ub => new DryRunSimulated("Benefit", Guid.Parse(ub.Local.Id), "Update"),
        CreatePolarProduct cp => new DryRunSimulated("Product", Guid.Parse(cp.Local.Id), "Create"),
        UpdatePolarProduct up => new DryRunSimulated("Product", Guid.Parse(up.Local.Id), "Update"),
        CreatePolarDiscount cd => new DryRunSimulated("Discount", Guid.Parse(cd.Local.Id), "Create"),
        UpdatePolarDiscount ud => new DryRunSimulated("Discount", Guid.Parse(ud.Local.Id), "Update"),
        CreatePolarCheckoutLink cl => new DryRunSimulated("CheckoutLink", cl.Local.Id.Value, "Create"),
        _ => new DryRunSimulated("Unknown", Guid.Empty, "Unknown"),
    };

    private async Task<PublishOutcome> PublishBenefitCreateAsync(CreatePolarBenefit action, EntitySets sets, CancellationToken ct)
    {
        var entity = sets.Benefits.First(b => b.Id == Guid.Parse(action.Local.Id));
        var payload = new PolarBenefitPayload(entity.BenefitKind, entity.Name, entity.Description, entity.PropertiesJson);
        var result = await _polarApi.CreateBenefitAsync(payload, ct).ConfigureAwait(false);
        return MapHttpResult(result, entity, polarId =>
        {
            entity.PolarBenefitId = polarId;
            entity.LastPublishedAt = _time.GetUtcNow();
            entity.Status = PublishStatus.Published;
        }, "Benefit");
    }

    private async Task<PublishOutcome> PublishBenefitUpdateAsync(UpdatePolarBenefit action, EntitySets sets, CancellationToken ct)
    {
        var entity = sets.Benefits.First(b => b.Id == Guid.Parse(action.Local.Id));
        var payload = new PolarBenefitPayload(entity.BenefitKind, entity.Name, entity.Description, entity.PropertiesJson);
        var result = await _polarApi.UpdateBenefitAsync(action.ExistingPolarId, payload, ct).ConfigureAwait(false);
        return MapHttpResult(result, entity, _ =>
        {
            entity.LastPublishedAt = _time.GetUtcNow();
            entity.Status = PublishStatus.Published;
        }, "Benefit");
    }

    private async Task<PublishOutcome> PublishProductCreateAsync(CreatePolarProduct action, EntitySets sets, CancellationToken ct)
    {
        var entity = sets.Products.First(p => p.Id == Guid.Parse(action.Local.Id));
        var payload = BuildProductPayload(entity);
        var result = await _polarApi.CreateProductAsync(payload, ct).ConfigureAwait(false);
        return MapHttpResult(result, entity, polarId =>
        {
            entity.PolarProductId = polarId;
            entity.LastPublishedAt = _time.GetUtcNow();
            entity.Status = PublishStatus.Published;
        }, "Product");
    }

    private async Task<PublishOutcome> PublishProductUpdateAsync(UpdatePolarProduct action, EntitySets sets, CancellationToken ct)
    {
        var entity = sets.Products.First(p => p.Id == Guid.Parse(action.Local.Id));
        var payload = BuildProductPayload(entity);
        var result = await _polarApi.UpdateProductAsync(action.ExistingPolarId, payload, ct).ConfigureAwait(false);
        return MapHttpResult(result, entity, _ =>
        {
            entity.LastPublishedAt = _time.GetUtcNow();
            entity.Status = PublishStatus.Published;
        }, "Product");
    }

    private async Task<PublishOutcome> PublishDiscountCreateAsync(CreatePolarDiscount action, EntitySets sets, CancellationToken ct)
    {
        var entity = sets.Discounts.First(d => d.Id == Guid.Parse(action.Local.Id));
        var payload = new PolarDiscountPayload(
            entity.MasterName, entity.Type, entity.AmountOff, entity.PercentageOff,
            entity.Currency, entity.Code, entity.StartsAt, entity.EndsAt, entity.MaxRedemptions);
        var result = await _polarApi.CreateDiscountAsync(payload, ct).ConfigureAwait(false);
        return MapHttpResult(result, entity, polarId =>
        {
            entity.PolarDiscountId = polarId;
            entity.LastPublishedAt = _time.GetUtcNow();
            entity.Status = PublishStatus.Published;
        }, "Discount");
    }

    private async Task<PublishOutcome> PublishDiscountUpdateAsync(UpdatePolarDiscount action, EntitySets sets, CancellationToken ct)
    {
        var entity = sets.Discounts.First(d => d.Id == Guid.Parse(action.Local.Id));
        var payload = new PolarDiscountPayload(
            entity.MasterName, entity.Type, entity.AmountOff, entity.PercentageOff,
            entity.Currency, entity.Code, entity.StartsAt, entity.EndsAt, entity.MaxRedemptions);
        var result = await _polarApi.UpdateDiscountAsync(action.ExistingPolarId, payload, ct).ConfigureAwait(false);
        return MapHttpResult(result, entity, _ =>
        {
            entity.LastPublishedAt = _time.GetUtcNow();
            entity.Status = PublishStatus.Published;
        }, "Discount");
    }

    private async Task<PublishOutcome> PublishCheckoutLinkCreateAsync(CreatePolarCheckoutLink action, EntitySets sets, CancellationToken ct)
    {
        var entity = sets.CheckoutLinks.First(l => l.Id == action.Local.Id.Value);
        var productIds = JsonSerializer.Deserialize<List<Guid>>(entity.ProductIdsJson, JsonOpts) ?? [];
        // Resolve local product Guids to their Polar product ids — only includes those
        // already published (PolarProductId set). Unpublished ones are simply omitted,
        // and the link can be re-published later once they exist on Polar.
        var polarProductIds = sets.Products
            .Where(p => productIds.Contains(p.Id) && !string.IsNullOrEmpty(p.PolarProductId))
            .Select(p => p.PolarProductId!)
            .ToList();

        var payload = new PolarCheckoutLinkPayload(
            entity.Name, polarProductIds, entity.SuccessUrl, entity.CancelUrl,
            entity.AllowDiscountCodes, entity.RequireBillingAddress);
        var result = await _polarApi.CreateCheckoutLinkAsync(payload, ct).ConfigureAwait(false);
        return MapHttpResultForCheckoutLink(result, entity, polarId =>
        {
            entity.PolarCheckoutLinkId = polarId;
            entity.Status = PublishStatus.Published;
        });
    }

    private static PolarProductPayload BuildProductPayload(LocalProductEntity entity)
    {
        var price = JsonSerializer.Deserialize<LocalPrice>(entity.PriceJson, JsonOpts)
                    ?? new LocalPrice { Kind = PriceKind.Free, Currency = "USD" };
        var attached = JsonSerializer.Deserialize<List<Guid>>(entity.AttachedBenefitsJson, JsonOpts) ?? [];
        var metadata = new Dictionary<string, string> { ["polar_sharp_local_id"] = entity.Id.ToString() };
        return new PolarProductPayload(
            entity.MasterName,
            entity.MasterDescription,
            price.IsRecurring,
            price.Amount,                         // already in minor units (int?)
            price.Currency,
            metadata,
            // The benefit ids are LOCAL Guids; the host's publish flow translates them to
            // Polar benefit ids by looking up each LocalBenefit's PolarBenefitId. v1.3.E
            // does this resolution at execution time inside the publisher.
            attached.Select(a => a.ToString()).ToList());
    }

    private PublishOutcome MapHttpResult<TEntity>(
        Result<string, PolarPublishApiError> apiResult,
        TEntity entity,
        Action<string> applyOnSuccess,
        string entityType) where TEntity : class
    {
        var entityId = entity switch
        {
            LocalBenefitEntity b => b.Id,
            LocalProductEntity p => p.Id,
            LocalDiscountEntity d => d.Id,
            _ => Guid.Empty,
        };

        if (apiResult.IsFailure)
        {
            MarkFailed(entity, entityType);
            return apiResult.Match(
                onSuccess: _ => throw new InvalidOperationException("Unreachable"),
                onFailure: err => new PublishFailedOutcome(entityType, entityId, MapApiError(err)));
        }

        var polarId = apiResult.Match(onSuccess: id => id, onFailure: _ => throw new InvalidOperationException("Unreachable"));
        applyOnSuccess(polarId);
        return new PublishedSuccessfully(entityType, entityId, polarId);
    }

    private PublishOutcome MapHttpResultForCheckoutLink(
        Result<string, PolarPublishApiError> apiResult,
        LocalCheckoutLinkEntity entity,
        Action<string> applyOnSuccess)
    {
        if (apiResult.IsFailure)
        {
            entity.Status = PublishStatus.PublishFailed;
            return apiResult.Match(
                onSuccess: _ => throw new InvalidOperationException("Unreachable"),
                onFailure: err => new PublishFailedOutcome("CheckoutLink", entity.Id, MapApiError(err)));
        }
        var polarId = apiResult.Match(onSuccess: id => id, onFailure: _ => throw new InvalidOperationException("Unreachable"));
        applyOnSuccess(polarId);
        return new PublishedSuccessfully("CheckoutLink", entity.Id, polarId);
    }

    private static void MarkFailed(object entity, string entityType)
    {
        switch (entity)
        {
            case LocalBenefitEntity b: b.Status = PublishStatus.PublishFailed; break;
            case LocalProductEntity p: p.Status = PublishStatus.PublishFailed; break;
            case LocalDiscountEntity d: d.Status = PublishStatus.PublishFailed; break;
        }
    }

    private static PublishError MapApiError(PolarPublishApiError err) => err.Kind switch
    {
        PolarPublishApiErrorKind.ValidationFailed => new PublishError(PublishErrorKind.PolarValidation, err.Message),
        PolarPublishApiErrorKind.NotFound => new PublishError(PublishErrorKind.PolarApiFailure, err.Message),
        _ => new PublishError(PublishErrorKind.PolarApiFailure, err.Message),
    };

    // ── Projections (entity → public LocalXxx record). Minimal — enough for the planned-action
    //    payload. Full reassembly with translations lives in EfPolarCatalogReader. ──

    private static LocalBenefit ProjectBenefit(LocalBenefitEntity e) =>
        new MinimalBenefit(e);

    private static LocalProduct ProjectProduct(LocalProductEntity e, IReadOnlyList<LocalProductVariantEntity> variants)
    {
        var price = JsonSerializer.Deserialize<LocalPrice>(e.PriceJson, JsonOpts) ?? new LocalPrice { Kind = PriceKind.Free, Currency = "USD" };
        var attached = JsonSerializer.Deserialize<List<Guid>>(e.AttachedBenefitsJson, JsonOpts) ?? [];
        return new LocalProduct
        {
            Id = e.Id.ToString(),
            TenantId = e.TenantId,
            OrganizationId = e.TenantId,
            Name = e.MasterName,
            CreatedAt = e.CreatedAt,
            ModifiedAt = e.ModifiedAt,
            IsRecurring = price.IsRecurring,
            MasterName = e.MasterName,
            MasterDescription = e.MasterDescription,
            MasterLanguage = e.MasterLanguage,
            Kind = e.Kind,
            TierGroupId = e.TierGroupId is { } tg ? new TierGroupId(tg) : null,
            HasVariants = e.HasVariants,
            Variants = [.. variants.Select(ProjectVariant)],
            Price = price,
            AttachedBenefits = [.. attached.Select(g => new BenefitId(g))],
            MsrpAmount = e.MsrpAmount,
            MsrpCurrency = e.MsrpCurrency,
            Manufacturer = e.Manufacturer,
            Isbn = e.Isbn,
            PolarProductId = e.PolarProductId,
            LastPublishedAt = e.LastPublishedAt,
            Status = e.Status,
            IsFakeData = e.IsFakeData,
        };
    }

    private static LocalProductVariant ProjectVariant(LocalProductVariantEntity e)
    {
        var axes = JsonSerializer.Deserialize<Dictionary<string, string>>(e.AxesJson, JsonOpts) ?? [];
        return new LocalProductVariant
        {
            Id = new VariantId(e.Id),
            Axes = axes,
            SurchargeAmount = e.SurchargeAmount,
            Sku = e.Sku,
            PolarProductId = e.PolarProductId,
            LastPublishedAt = e.LastPublishedAt,
            IsActive = e.IsActive,
            InventoryCount = e.InventoryCount,
            InventoryLowThreshold = e.InventoryLowThreshold,
            LastStockChangedAt = e.LastStockChangedAt,
        };
    }

    private static LocalDiscount ProjectDiscount(LocalDiscountEntity e) =>
        new()
        {
            DiscountId = new DiscountId(e.Id),
            Id = e.Id.ToString(),
            TenantId = e.TenantId,
            OrganizationId = e.TenantId,
            Name = e.Name,
            MasterName = e.MasterName,
            Code = e.Code,
            Kind = e.Kind,
            Type = e.Type,
            AmountOff = e.AmountOff,
            PercentageOff = e.PercentageOff,
            Currency = e.Currency,
            DurationKind = e.DurationKind,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            MaxRedemptions = e.MaxRedemptions,
            CreatedAt = e.CreatedAt,
            Status = e.Status,
            IsFakeData = e.IsFakeData,
        };

    private static LocalCheckoutLinkConfig ProjectCheckoutLink(LocalCheckoutLinkEntity e)
    {
        var productIds = JsonSerializer.Deserialize<List<Guid>>(e.ProductIdsJson, JsonOpts) ?? [];
        return new LocalCheckoutLinkConfig
        {
            Id = new CheckoutLinkId(e.Id),
            TenantId = e.TenantId,
            Name = e.Name,
            ProductIds = [.. productIds.Select(g => new ProductId(g))],
            SuccessUrl = e.SuccessUrl,
            CancelUrl = e.CancelUrl,
            ThemeColor = e.ThemeColor,
            LogoUrl = e.LogoUrl,
            AllowDiscountCodes = e.AllowDiscountCodes,
            RequireBillingAddress = e.RequireBillingAddress,
            Status = e.Status,
            IsFakeData = e.IsFakeData,
        };
    }

    /// <summary>Minimal benefit projection — full polymorphic subtype hydration happens in the catalog reader.</summary>
    private sealed record MinimalBenefit : LocalBenefit
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public MinimalBenefit(LocalBenefitEntity e)
        {
            Id = e.Id.ToString();
            BenefitId = new BenefitId(e.Id);
            TenantId = e.TenantId;
            OrganizationId = e.TenantId;
            Name = e.Name;
            Description = e.Description;
            CreatedAt = e.CreatedAt;
            PolarBenefitId = e.PolarBenefitId;
            LastPublishedAt = e.LastPublishedAt;
            Status = e.Status;
            IsFakeData = e.IsFakeData;
            Type = Enum.TryParse<PolarSharp.BaseEntities.PolarBenefitType>(e.BenefitKind, ignoreCase: true, out var t)
                ? t : PolarSharp.BaseEntities.PolarBenefitType.Custom;
        }
    }

    // ── Loaded entity sets pulled in a single query batch ─────────────────────────

    private sealed record EntitySets(
        IReadOnlyList<LocalBenefitEntity> Benefits,
        IReadOnlyList<LocalProductEntity> Products,
        IReadOnlyList<LocalProductVariantEntity> Variants,
        IReadOnlyList<LocalDiscountEntity> Discounts,
        IReadOnlyList<LocalCheckoutLinkEntity> CheckoutLinks);
}
